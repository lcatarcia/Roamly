using System.IO.Compression;
using System.IO.Pipelines;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Ownership;
using Roamly.Api.Common.Security;
using Roamly.Common;
using Roamly.Common.Storage;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Identity.AccountExport;

/// <summary>Prepara l'export solo dopo login/password avvenuto negli ultimi cinque minuti.</summary>
[ProtocolOutcomeHandler("Credenziali non fresche e rate limit hanno esiti HTTP distinti.")]
internal sealed class AccountExportHandler
{
    private readonly RoamlyDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPerAccountRateLimiter _rateLimiter;
    private readonly OwnedResourceWalker _walker;
    private readonly TimeProvider _clock;
    private readonly IJournalImageStorage _imageStorage;

    public AccountExportHandler(
        RoamlyDbContext db,
        ICurrentUser currentUser,
        IPerAccountRateLimiter rateLimiter,
        OwnedResourceWalker walker,
        TimeProvider clock,
        IJournalImageStorage imageStorage)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(walker);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(imageStorage);
        _db = db;
        _currentUser = currentUser;
        _rateLimiter = rateLimiter;
        _walker = walker;
        _clock = clock;
        _imageStorage = imageStorage;
    }

    public AccountExportPreparation Prepare(ClaimsPrincipal principal)
    {
        if (!PasswordAuthenticationClaims.IsFresh(principal, _clock.GetUtcNow()))
        {
            return new AccountExportPreparation(AccountExportOutcome.PasswordAuthenticationRequired, null);
        }

        if (!_rateLimiter.TryAcquire($"account-export:{_currentUser.Id:D}"))
        {
            return new AccountExportPreparation(AccountExportOutcome.TooManyAttempts, null);
        }

        var generatedAtUtc = _clock.GetUtcNow().UtcDateTime;
        return new AccountExportPreparation(
            AccountExportOutcome.Ready,
            new AccountExportZipResult(_db, _walker, _imageStorage, _currentUser.Id, generatedAtUtc));
    }
}

internal enum AccountExportOutcome
{
    Ready,
    PasswordAuthenticationRequired,
    TooManyAttempts,
}

internal sealed record AccountExportPreparation(AccountExportOutcome Outcome, IResult? Result);

/// <summary>Risultato HTTP che scrive lo ZIP direttamente nel body della risposta.</summary>
internal sealed class AccountExportZipResult(
    RoamlyDbContext db,
    OwnedResourceWalker walker,
    IJournalImageStorage imageStorage,
    Guid ownerId,
    DateTime generatedAtUtc) : IResult
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/zip";
        context.Response.Headers["Content-Disposition"] = "attachment; filename=\"roamly-export.zip\"";
        context.Response.Headers.CacheControl = "no-store";

        try
        {
            await context.Response.StartAsync(context.RequestAborted).ConfigureAwait(false);
            await WriteArchiveAsync(context).ConfigureAwait(false);
            await context.Response.BodyWriter.FlushAsync(context.RequestAborted).ConfigureAwait(false);
        }
        catch
        {
            context.Abort();
            throw;
        }
    }

    private async Task WriteArchiveAsync(HttpContext context)
    {
        var files = new List<AccountExportFile>();
        var responseWriter = context.Response.BodyWriter;
        using (var archive = new ZipArchive(
            responseWriter.AsStream(leaveOpen: true),
            ZipArchiveMode.Create,
            leaveOpen: true))
        {
            foreach (var entityType in OwnedResourceWalker.GetEntityTypes(db.Model))
            {
                var path = $"entities/{entityType.ClrType.Name}.json";
                var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
                long count;
                using (var entryStream = entry.Open())
                {
                    count = await walker.WriteJsonArrayAsync(
                        db,
                        entityType,
                        ownerId,
                        entryStream,
                        responseWriter,
                        context.RequestAborted).ConfigureAwait(false);
                }

                files.Add(new AccountExportFile(entityType.ClrType.Name, path, count));
            }

            var attachmentCount = await WriteJournalAttachmentsAsync(archive, context).ConfigureAwait(false);
            files.Add(new AccountExportFile(
                "JournalEntryAttachmentBlob",
                "attachments/{JournalEntryId}/{AttachmentId}",
                attachmentCount));
            await WriteManifestAsync(archive, files, context.RequestAborted).ConfigureAwait(false);
        }
    }

    private async Task<long> WriteJournalAttachmentsAsync(ZipArchive archive, HttpContext context)
    {
        long count = 0;
        var attachments = db.JournalEntryAttachments
            .AsNoTracking()
            .Where(attachment => attachment.OwnerId == ownerId)
            .OrderBy(attachment => attachment.Id)
            .Select(attachment => new
            {
                attachment.Id,
                attachment.JournalEntryId,
                attachment.StorageKey,
            })
            .AsAsyncEnumerable();

        await foreach (var attachment in attachments
            .WithCancellation(context.RequestAborted).ConfigureAwait(false))
        {
            var path = $"attachments/{attachment.JournalEntryId:D}/{attachment.Id:D}";
            var blob = await imageStorage.OpenReadAsync(
                attachment.StorageKey, context.RequestAborted).ConfigureAwait(false);
            if (blob is null)
            {
                throw new IOException(
                    $"Journal image {attachment.Id:D} is missing from Blob Storage; the export is incomplete.");
            }

            await using (blob.ConfigureAwait(false))
            {
                var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
                await using var destination = entry.Open();
                await blob.CopyToAsync(destination, context.RequestAborted).ConfigureAwait(false);
            }

            count++;
        }

        return count;
    }

    private async Task WriteManifestAsync(
        ZipArchive archive,
        IReadOnlyList<AccountExportFile> files,
        CancellationToken cancellationToken)
    {
        var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Fastest);
        await using var manifestStream = manifestEntry.Open();
        await JsonSerializer.SerializeAsync(
            manifestStream,
            new AccountExportManifest(1, generatedAtUtc, files),
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
    }
}

internal sealed record AccountExportManifest(
    int FormatVersion,
    DateTime GeneratedAtUtc,
    IReadOnlyList<AccountExportFile> Files);

internal sealed record AccountExportFile(string EntityType, string Path, long Count);
