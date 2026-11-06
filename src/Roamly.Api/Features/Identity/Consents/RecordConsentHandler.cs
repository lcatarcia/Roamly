using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Ownership;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Identity.Consents;

/// <summary>Appende una prova dopo lock e recheck account; nessun lock o chiamata remota fuori transazione.</summary>
[ProtocolOutcomeHandler("Retry identico 200, collisione/stato obsoleto o finalita' inattiva 409; restrizioni account 403.")]
public sealed class RecordConsentHandler(
    RoamlyDbContext db, ConsentPurposeDefinition definition, TimeProvider clock)
{
    public async Task<RecordConsentResult> HandleAsync(
        string purpose, string action, RecordConsentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var account = await AccountDeletionStateOperations.LockAsync(
            db, db.CurrentUserId, cancellationToken).ConfigureAwait(false);
        if (account is null || account.RequiresPasswordChange || account.DeletionRequestedAtUtc is not null)
        {
            return new(RecordConsentOutcome.AccountRestricted, null);
        }

        var result = await RecordAsync(purpose, action, request, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<RecordConsentResult> RecordAsync(
        string purpose, string action, RecordConsentRequest request, CancellationToken cancellationToken)
    {
        var existing = await db.ConsentTransitions.AsNoTracking().SingleOrDefaultAsync(
            item => item.OwnerId == db.CurrentUserId && item.Id == request.Id!.Value,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Replay(existing, purpose, action, request);
        }

        var rejection = CheckDefinition(purpose, action, request);
        return rejection is not null
            ? new(rejection.Value, null)
            : await AppendAsync(purpose, action, request, cancellationToken).ConfigureAwait(false);
    }

    private RecordConsentOutcome? CheckDefinition(string purpose, string action, RecordConsentRequest request)
    {
        if (purpose != definition.Purpose)
        {
            return RecordConsentOutcome.NotFound;
        }

        if (request.Version != definition.Version || request.TextReference != definition.TextReference)
        {
            return RecordConsentOutcome.DefinitionChanged;
        }

        return action == "grant" && !definition.IsActive ? RecordConsentOutcome.Inactive : null;
    }

    private async Task<RecordConsentResult> AppendAsync(
        string purpose, string action, RecordConsentRequest request, CancellationToken cancellationToken)
    {
        var previous = await db.ConsentTransitions.AsNoTracking()
            .Where(item => item.OwnerId == db.CurrentUserId && item.Purpose == purpose)
            .MaxAsync(item => (long?)item.Revision, cancellationToken).ConfigureAwait(false) ?? 0;
        var transition = new ConsentTransition
        {
            Id = request.Id!.Value, OwnerId = db.CurrentUserId, Purpose = purpose,
            Version = request.Version!, TextReference = request.TextReference!, Action = action,
            Revision = checked(previous + 1),
            CreatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(clock.GetUtcNow().ToUnixTimeMilliseconds()).UtcDateTime,
        };
        db.ConsentTransitions.Add(transition);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(RecordConsentOutcome.Created, ConsentReceiptResponse.From(transition));
    }

    private static RecordConsentResult Replay(
        ConsentTransition existing, string purpose, string action, RecordConsentRequest request) =>
        existing.Purpose == purpose && existing.Action == action
        && existing.Version == request.Version && existing.TextReference == request.TextReference
            ? new(RecordConsentOutcome.Replayed, ConsentReceiptResponse.From(existing))
            : new(RecordConsentOutcome.Conflict, null);
}

public sealed record RecordConsentResult(RecordConsentOutcome Outcome, ConsentReceiptResponse? Receipt);

public enum RecordConsentOutcome
{
    Created, Replayed, Conflict, NotFound, Inactive, DefinitionChanged, AccountRestricted,
}

/// <summary>Ricevuta storica, non stato corrente: un replay non rende di nuovo efficace un grant revocato.</summary>
public sealed record ConsentReceiptResponse(
    Guid Id, string Purpose, string Version, string TextReference, string Action, long Revision, DateTime RecordedAtUtc)
{
    internal static ConsentReceiptResponse From(ConsentTransition item) =>
        new(item.Id, item.Purpose, item.Version, item.TextReference, item.Action, item.Revision,
            DateTime.SpecifyKind(item.CreatedAtUtc, DateTimeKind.Utc));
}
