using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roamly.Common;
using Roamly.Common.Storage;
using Roamly.Api.Common.Ownership;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.Infrastructure.Storage;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Trips;

[Collection(DatabaseCollection.Name)]
public sealed class JournalEntryApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Password = "Test!Password123";
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly JournalDeleteCasConflictInterceptor _deleteCasConflict = new();
    private FailOnceJournalImageStorage _imageStorage = null!;
    private RoamlyApiFactory _factory = null!;
    private RoamlyClient _client = null!;
    private DatabaseLease _database = null!;

    public async ValueTask InitializeAsync()
    {
        var migrated = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(migrated.Failure);
        _database = migrated.Lease;
        await _database.ResetAsync(TestContext.Current.CancellationToken);
        _deleteCasConflict.Configure(_database.ConnectionString);
        _imageStorage = new FailOnceJournalImageStorage(
            new AzureJournalImageStorage(
                new BlobServiceClient(sqlServer.JournalImageStorageConnectionString),
                new JournalImageStorageSettings(
                    "Azurite",
                    "roamly-journal-images",
                    sqlServer.JournalImageStorageConnectionString,
                    null,
                    AzureAccountConfigurationVerified: true),
                TimeProvider.System));

        _factory = new RoamlyApiFactory(
            _database.ConnectionString,
            configureTestServices: services =>
            {
                services.ConfigureDbContext<RoamlyDbContext>(
                    options => options.AddInterceptors(_deleteCasConflict));
                services.RemoveAll<IJournalImageStorage>();
                services.AddSingleton<IJournalImageStorage>(_imageStorage);
            },
            journalImagesAzuriteConnectionString: sqlServer.JournalImageStorageConnectionString);
        _client = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        await CreateUserAsync();
        using var login = await _client.LoginAsync(
            "journal-owner@example.test",
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task traveller_can_create_a_journal_entry_for_a_trip_and_read_it_back()
    {
        var (tripId, tripEtag) = await CreateTripAsync();
        var entryId = NewId();
        using var created = await PostJournalEntryWithEtagAsync(
            $"/api/v1/trips/{tripId:D}/journal-entries",
            new
            {
                id = entryId,
                entryOnUtc = "2026-10-09",
                title = "Arrivo a Saint-Malo",
                body = "Abbiamo parcheggiato vicino al porto.",
            },
            tripEtag,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}",
            created.Headers.Location?.OriginalString);

        using var read = await _client.GetAsync(
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var details = JsonDocument.Parse(
            await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(entryId, details.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(tripId, details.RootElement.GetProperty("tripId").GetGuid());
        Assert.Equal("Arrivo a Saint-Malo", details.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            "Abbiamo parcheggiato vicino al porto.",
            details.RootElement.GetProperty("body").GetString());
    }

    [Fact]
    public async Task traveller_can_list_update_and_delete_a_journal_entry_with_trip_etags()
    {
        var (tripId, tripEtag) = await CreateTripAsync();
        var entryId = NewId();
        using var created = await PostJournalEntryWithEtagAsync(
            $"/api/v1/trips/{tripId:D}/journal-entries",
            new
            {
                id = entryId,
                entryOnUtc = "2026-10-09",
                title = "Prima versione",
                body = "Appunti iniziali.",
            },
            tripEtag,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var currentEtag = Assert.IsType<string>(created.Headers.ETag?.ToString());

        using var listed = await _client.GetAsync(
            $"/api/v1/trips/{tripId:D}/journal-entries?limit=20",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var listBody = JsonDocument.Parse(
            await listed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(
            listBody.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == entryId);

        using var updated = await SendJsonWithEtagAsync(
            HttpMethod.Put,
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}",
            new
            {
                entryOnUtc = "2026-10-10",
                title = "Versione aggiornata",
                body = "Abbiamo aggiunto un dettaglio.",
            },
            currentEtag,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        currentEtag = Assert.IsType<string>(updated.Headers.ETag?.ToString());
        using var updatedBody = JsonDocument.Parse(
            await updated.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "Versione aggiornata",
            updatedBody.RootElement.GetProperty("title").GetString());

        using var deleted = await SendJsonWithEtagAsync(
            HttpMethod.Delete,
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}",
            null,
            currentEtag,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var missing = await _client.GetAsync(
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task attachment_delete_cas_conflict_keeps_blob_and_does_not_partially_delete_sql_rows()
    {
        var (tripId, tripEtag) = await CreateTripAsync();
        var (entryId, entryEtag) = await CreateJournalEntryAsync(tripId, tripEtag);
        using var uploadResponse = await UploadAttachmentAsync(tripId, entryId, entryEtag);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        using var uploadBody = JsonDocument.Parse(
            await uploadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var attachmentId = uploadBody.RootElement.GetProperty("id").GetGuid();
        var currentTripEtag = Assert.IsType<string>(uploadResponse.Headers.ETag?.ToString());
        var storageKey = $"{_ownerId:D}/journal-entry-attachment/{entryId:D}/{attachmentId:D}";

        _deleteCasConflict.Arm(_ownerId, tripId);
        using var deletion = await SendJsonWithEtagAsync(
            HttpMethod.Delete,
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}/attachments/{attachmentId:D}",
            null,
            currentTripEtag,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.PreconditionFailed, deletion.StatusCode);
        var blob = new BlobServiceClient(sqlServer.JournalImageStorageConnectionString)
            .GetBlobContainerClient("roamly-journal-images")
            .GetBlobClient(storageKey);
        Assert.True((await blob.ExistsAsync(TestContext.Current.CancellationToken)).Value);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoamlyDbContext>();
        Assert.True(await db.JournalEntryAttachments.IgnoreQueryFilters().AnyAsync(
            attachment => attachment.OwnerId == _ownerId && attachment.Id == attachmentId,
            TestContext.Current.CancellationToken));
        Assert.Equal(0, await db.JournalAttachmentDeletionOutbox.IgnoreQueryFilters()
            .CountAsync(message => message.OwnerId == _ownerId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task attachment_http_flow_is_owner_scoped_and_blob_deletion_retries_idempotently()
    {
        var (tripId, tripEtag) = await CreateTripAsync();
        var (entryId, entryEtag) = await CreateJournalEntryAsync(tripId, tripEtag);
        using var extraSection = await UploadAttachmentAsync(
            _client, tripId, entryId, entryEtag, includeExtraSection: true);
        Assert.Equal(HttpStatusCode.BadRequest, extraSection.StatusCode);
        using var uploaded = await UploadAttachmentAsync(tripId, entryId, entryEtag);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        using var uploadBody = JsonDocument.Parse(
            await uploaded.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var attachmentId = uploadBody.RootElement.GetProperty("id").GetGuid();
        var currentEtag = Assert.IsType<string>(uploaded.Headers.ETag?.ToString());
        var storageKey = $"{_ownerId:D}/journal-entry-attachment/{entryId:D}/{attachmentId:D}";
        var blob = new BlobServiceClient(sqlServer.JournalImageStorageConnectionString)
            .GetBlobContainerClient("roamly-journal-images")
            .GetBlobClient(storageKey);

        using var metadata = await _client.GetAsync(
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}/attachments/{attachmentId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, metadata.StatusCode);
        using var download = await _client.GetAsync(
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}/attachments/{attachmentId:D}/download",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        using var downloadBody = JsonDocument.Parse(
            await download.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains("sp=r", downloadBody.RootElement.GetProperty("url").GetString(), StringComparison.Ordinal);

        await CreateUserAsync("journal-foreign@example.test", Guid.NewGuid());
        using var foreignClient = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        using var foreignLogin = await foreignClient.LoginAsync(
            "journal-foreign@example.test",
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, foreignLogin.StatusCode);
        using var foreignUpload = await UploadAttachmentAsync(
            foreignClient, tripId, entryId, currentEtag);
        Assert.Equal(HttpStatusCode.NotFound, foreignUpload.StatusCode);
        using var foreignRead = await foreignClient.GetAsync(
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}/attachments/{attachmentId:D}/download",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreignRead.StatusCode);

        using var deleted = await SendJsonWithEtagAsync(
            HttpMethod.Delete,
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}/attachments/{attachmentId:D}",
            null,
            currentEtag,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.True((await blob.ExistsAsync(TestContext.Current.CancellationToken)).Value);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoamlyDbContext>();
            Assert.Equal(1, await db.JournalAttachmentDeletionOutbox.IgnoreQueryFilters()
                .CountAsync(message => message.OwnerId == _ownerId, TestContext.Current.CancellationToken));
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            _imageStorage.FailNextDelete();
            var job = scope.ServiceProvider.GetRequiredService<JournalAttachmentDeletionOutboxJob>();
            var failed = await Assert.ThrowsAsync<AggregateException>(
                () => job.RunOnceAsync(TestContext.Current.CancellationToken));
            Assert.Single(failed.InnerExceptions);
            Assert.True((await blob.ExistsAsync(TestContext.Current.CancellationToken)).Value);
            await scope.ServiceProvider.GetRequiredService<JournalAttachmentDeletionOutboxJob>()
                .RunOnceAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<JournalAttachmentDeletionOutboxJob>()
                .RunOnceAsync(TestContext.Current.CancellationToken);
            var db = scope.ServiceProvider.GetRequiredService<RoamlyDbContext>();
            Assert.Equal(0, await db.JournalAttachmentDeletionOutbox.IgnoreQueryFilters()
                .CountAsync(message => message.OwnerId == _ownerId, TestContext.Current.CancellationToken));
        }

        Assert.False((await blob.ExistsAsync(TestContext.Current.CancellationToken)).Value);
    }

    [Fact]
    public async Task deleting_entry_queues_each_cascaded_attachment_for_blob_cleanup()
    {
        var (tripId, tripEtag) = await CreateTripAsync();
        var (entryId, entryEtag) = await CreateJournalEntryAsync(tripId, tripEtag);
        var storageKeys = new List<string>();
        var currentEtag = entryEtag;
        for (var index = 0; index < 2; index++)
        {
            using var uploaded = await UploadAttachmentAsync(tripId, entryId, currentEtag);
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
            using var body = JsonDocument.Parse(
                await uploaded.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            storageKeys.Add(
                $"{_ownerId:D}/journal-entry-attachment/{entryId:D}/{body.RootElement.GetProperty("id").GetGuid():D}");
            currentEtag = Assert.IsType<string>(uploaded.Headers.ETag?.ToString());
        }

        using var deleted = await SendJsonWithEtagAsync(
            HttpMethod.Delete,
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}",
            null,
            currentEtag,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var container = new BlobServiceClient(sqlServer.JournalImageStorageConnectionString)
            .GetBlobContainerClient("roamly-journal-images");
        foreach (var key in storageKeys)
        {
            Assert.True((await container.GetBlobClient(key)
                .ExistsAsync(TestContext.Current.CancellationToken)).Value);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoamlyDbContext>();
            Assert.Equal(2, await db.JournalAttachmentDeletionOutbox.IgnoreQueryFilters()
                .CountAsync(message => message.OwnerId == _ownerId, TestContext.Current.CancellationToken));
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<JournalAttachmentDeletionOutboxJob>()
                .RunOnceAsync(TestContext.Current.CancellationToken);
        }

        foreach (var key in storageKeys)
        {
            Assert.False((await container.GetBlobClient(key)
                .ExistsAsync(TestContext.Current.CancellationToken)).Value);
        }
    }

    private Task CreateUserAsync() => CreateUserAsync("journal-owner@example.test", _ownerId);

    private async Task CreateUserAsync(string email, Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await userManager.CreateAsync(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAtUtc = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
        }, Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private async Task<(Guid Id, string Etag)> CreateTripAsync()
    {
        var camperId = NewId();
        using var camper = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id = camperId,
            name = "Il Grigio",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, camper.StatusCode);

        var tripId = NewId();
        using var trip = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/trips",
            new { id = tripId, name = "Weekend in Bretagna", stops = new[] { new { name = "Saint-Malo" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, trip.StatusCode);
        using var tripDetails = await _client.GetAsync(
            $"/api/v1/trips/{tripId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, tripDetails.StatusCode);
        return (tripId, Assert.IsType<string>(tripDetails.Headers.ETag?.ToString()));
    }

    private async Task<(Guid Id, string Etag)> CreateJournalEntryAsync(Guid tripId, string etag)
    {
        var entryId = NewId();
        using var created = await PostJournalEntryWithEtagAsync(
            $"/api/v1/trips/{tripId:D}/journal-entries",
            new
            {
                id = entryId,
                entryOnUtc = "2026-10-09",
                body = "Nota con immagine.",
            },
            etag,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (entryId, Assert.IsType<string>(created.Headers.ETag?.ToString()));
    }

    private Task<HttpResponseMessage> UploadAttachmentAsync(
        Guid tripId,
        Guid entryId,
        string etag) =>
        UploadAttachmentAsync(_client, tripId, entryId, etag);

    private static async Task<HttpResponseMessage> UploadAttachmentAsync(
        RoamlyClient client,
        Guid tripId,
        Guid entryId,
        string etag,
        bool includeExtraSection = false)
    {
        using var csrfResponse = await client.GetAsync("/api/v1/csrf-token", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        using var csrfBody = JsonDocument.Parse(
            await csrfResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}/attachments");
        var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xD9]), "file", "photo.jpg");
        if (includeExtraSection)
        {
            multipart.Add(new ByteArrayContent([0x01]), "extra", "extra.txt");
        }

        request.Content = multipart;
        request.Headers.Add("X-Roamly-Request", "1");
        request.Headers.Add("X-XSRF-TOKEN", csrfBody.RootElement.GetProperty("token").GetString());
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private Task<HttpResponseMessage> PostJournalEntryWithEtagAsync(
        string path,
        object requestBody,
        string etag,
        CancellationToken cancellationToken) =>
        SendJsonWithEtagAsync(HttpMethod.Post, path, requestBody, etag, cancellationToken);

    private async Task<HttpResponseMessage> SendJsonWithEtagAsync(
        HttpMethod method,
        string path,
        object? requestBody,
        string etag,
        CancellationToken cancellationToken)
    {
        using var csrfResponse = await _client.GetAsync("/api/v1/csrf-token", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        using var csrfBody = JsonDocument.Parse(
            await csrfResponse.Content.ReadAsStringAsync(cancellationToken));
        using var request = new HttpRequestMessage(method, path);
        if (requestBody is not null)
        {
            request.Content = JsonContent.Create(requestBody);
        }
        request.Headers.Add("X-Roamly-Request", "1");
        request.Headers.Add(
            "X-XSRF-TOKEN",
            csrfBody.RootElement.GetProperty("token").GetString());
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));
        return await _client.SendAsync(request, cancellationToken);
    }

    private static Guid NewId() => new SequentialGuidGenerator(TimeProvider.System).NewId();

    private sealed class JournalDeleteCasConflictInterceptor : SaveChangesInterceptor
    {
        private string _connectionString = string.Empty;
        private Guid _ownerId;
        private Guid _tripId;
        private int _armed;

        public void Configure(string connectionString) => _connectionString = connectionString;

        public void Arm(Guid ownerId, Guid tripId)
        {
            _ownerId = ownerId;
            _tripId = tripId;
            Interlocked.Exchange(ref _armed, 1);
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _armed, 0) == 1
                && eventData.Context?.ChangeTracker.Entries<JournalEntryAttachment>()
                    .Any(entry => entry.State == EntityState.Deleted) == true)
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "UPDATE [dbo].[Trips] SET [UpdatedAtUtc] = SYSUTCDATETIME() "
                    + "WHERE [OwnerId] = @ownerId AND [Id] = @tripId;";
                command.Parameters.AddWithValue("@ownerId", _ownerId);
                command.Parameters.AddWithValue("@tripId", _tripId);
                Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
            }

            return result;
        }
    }

    private sealed class FailOnceJournalImageStorage(IJournalImageStorage inner) : IJournalImageStorage
    {
        private int _failNextDelete;

        public void FailNextDelete() => Interlocked.Exchange(ref _failNextDelete, 1);

        public Task UploadAsync(
            string key,
            Stream content,
            string contentType,
            CancellationToken cancellationToken) =>
            inner.UploadAsync(key, content, contentType, cancellationToken);

        public Task<JournalImageDownloadLink?> CreateReadOnlyDownloadLinkAsync(
            string key,
            CancellationToken cancellationToken) =>
            inner.CreateReadOnlyDownloadLinkAsync(key, cancellationToken);

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) =>
            inner.OpenReadAsync(key, cancellationToken);

        public Task DeleteIfExistsAsync(string key, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _failNextDelete, 0) == 1)
            {
                throw new InvalidOperationException("Injected transient Blob deletion failure.");
            }

            return inner.DeleteIfExistsAsync(key, cancellationToken);
        }

        public Task<long> DeleteByPrefixAsync(string ownerPrefix, CancellationToken cancellationToken) =>
            inner.DeleteByPrefixAsync(ownerPrefix, cancellationToken);

        public IAsyncEnumerable<string> ListKeysAsync(string prefix, CancellationToken cancellationToken) =>
            inner.ListKeysAsync(prefix, cancellationToken);
    }
}
