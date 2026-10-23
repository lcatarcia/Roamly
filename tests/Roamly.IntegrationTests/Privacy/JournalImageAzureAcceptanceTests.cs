using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Common.Storage;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;
using Roamly.Api.Common.Ownership;

namespace Roamly.IntegrationTests.Privacy;

[Collection(DatabaseCollection.Name)]
public sealed class JournalImageAzureAcceptanceTests(SqlServerFixture sqlServer)
{
    private const string OptInEnvironmentVariable = "ROAMLY_AZURE_BLOB_ACCEPTANCE";
    private const string AccountConfigurationVerifiedEnvironmentVariable =
        "ROAMLY_AZURE_BLOB_ACCOUNT_CONFIGURATION_VERIFIED";
    private const string Password = "Test!Password123";
    private static readonly byte[] SyntheticImageBytes = [0xFF, 0xD8, 0xFF, 0xD9];

    [Fact]
    public async Task Azure_blob_api_flow_uploads_downloads_exports_and_erases_an_ephemeral_owner()
    {
        var acceptanceOptIn = string.Equals(
            Environment.GetEnvironmentVariable(OptInEnvironmentVariable),
            "1",
            StringComparison.Ordinal);
        var azureAccountConfigurationVerified = string.Equals(
            Environment.GetEnvironmentVariable(AccountConfigurationVerifiedEnvironmentVariable),
            "1",
            StringComparison.Ordinal);
        Assert.SkipUnless(
            acceptanceOptIn && azureAccountConfigurationVerified,
            $"Set {OptInEnvironmentVariable}=1 and " +
            $"{AccountConfigurationVerifiedEnvironmentVariable}=1 to run this real Azure Blob acceptance test. " +
            "Before running it, manually verify the effective Azure account configuration; this test does not " +
            "verify Azure Blob versioning or retention settings.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var ownerId = Guid.NewGuid();
        var ownerPrefix = $"{ownerId:D}/";
        var email = $"azure-acceptance-{ownerId:N}@example.test";
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var migrated = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(migrated.Failure);
        var database = migrated.Lease;
        await database.ResetAsync(cancellationToken);

        RoamlyApiFactory? factory = null;
        Exception? testFailure = null;
        var cleanupFailures = new List<Exception>();
        try
        {
            factory = new RoamlyApiFactory(
                database.ConnectionString,
                useAzureBlobJournalImageStorage: true,
                azureBlobAccountConfigurationVerified: azureAccountConfigurationVerified);
            using var client = new RoamlyClient(factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true,
            }));

            await CreateUserAsync(factory, ownerId, email);
            using (var login = await client.LoginAsync(email, Password, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            }

            var (tripId, tripEtag) = await CreateTripAsync(client, cancellationToken);
            var (entryId, entryEtag) = await CreateJournalEntryAsync(
                client,
                tripId,
                tripEtag,
                cancellationToken);
            var firstAttachment = await UploadAttachmentAsync(
                client,
                tripId,
                entryId,
                entryEtag,
                cancellationToken);
            var secondAttachment = await UploadAttachmentAsync(
                client,
                tripId,
                entryId,
                firstAttachment.Etag,
                cancellationToken);

            await DownloadThroughReadOnlySasAsync(
                client,
                tripId,
                entryId,
                firstAttachment.Id,
                cancellationToken);
            await AssertExportContainsImageAsync(
                client,
                entryId,
                firstAttachment.Id,
                cancellationToken);

            using (var deletion = await SendWithEtagAsync(
                client,
                HttpMethod.Delete,
                $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}/attachments/{firstAttachment.Id:D}",
                null,
                secondAttachment.Etag,
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
            }

            var deletedStorageKey =
                $"{ownerId:D}/journal-entry-attachment/{entryId:D}/{firstAttachment.Id:D}";
            var blobService = factory.Services.GetRequiredService<BlobServiceClient>();
            var container = blobService.GetBlobContainerClient("roamly-journal-images");
            var deletedBlob = container.GetBlobClient(deletedStorageKey);
            await using (var verification = factory.Services.CreateAsyncScope())
            {
                var db = verification.ServiceProvider.GetRequiredService<RoamlyDbContext>();
                Assert.False(await db.JournalEntryAttachments
                    .IgnoreQueryFilters()
                    .AnyAsync(
                        attachment => attachment.OwnerId == ownerId
                            && attachment.StorageKey == deletedStorageKey,
                        cancellationToken));
                Assert.True(await db.JournalAttachmentDeletionOutbox
                    .IgnoreQueryFilters()
                    .AnyAsync(
                        message => message.OwnerId == ownerId
                            && message.StorageKey == deletedStorageKey,
                        cancellationToken));
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<JournalAttachmentDeletionOutboxJob>()
                    .RunOnceAsync(cancellationToken);
            }

            await using (var verification = factory.Services.CreateAsyncScope())
            {
                var db = verification.ServiceProvider.GetRequiredService<RoamlyDbContext>();
                Assert.False(await db.JournalAttachmentDeletionOutbox
                    .IgnoreQueryFilters()
                    .AnyAsync(
                        message => message.OwnerId == ownerId
                            && message.StorageKey == deletedStorageKey,
                        cancellationToken));
            }

            Assert.False((await deletedBlob.ExistsAsync(cancellationToken)).Value);

            await ScheduleImmediateErasureAsync(factory, ownerId, now, cancellationToken);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<AccountErasureJob>()
                    .RunOnceAsync(cancellationToken);
            }

            await using (var verification = factory.Services.CreateAsyncScope())
            {
                var db = verification.ServiceProvider.GetRequiredService<RoamlyDbContext>();
                Assert.False(await db.Users.AnyAsync(user => user.Id == ownerId, cancellationToken));
                var receipt = await db.ErasureReceipts.SingleAsync(cancellationToken);
                Assert.True(receipt.StorageSweepConfirmed);
            }

            var remainingOwnerBlobs = new List<string>();
            await foreach (var blob in container.GetBlobsAsync(
                BlobTraits.None,
                BlobStates.None,
                prefix: ownerPrefix,
                cancellationToken: cancellationToken))
            {
                remainingOwnerBlobs.Add(blob.Name);
            }

            Assert.Empty(remainingOwnerBlobs);
        }
        catch (Exception exception)
        {
            testFailure = exception;
        }
        finally
        {
            if (factory is not null)
            {
                try
                {
                    await using var cleanup = factory.Services.CreateAsyncScope();
                    await cleanup.ServiceProvider.GetRequiredService<IJournalImageStorage>()
                        .DeleteByPrefixAsync(ownerPrefix, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(exception);
                }
                finally
                {
                    try
                    {
                        await factory.DisposeAsync();
                    }
                    catch (Exception exception)
                    {
                        cleanupFailures.Add(exception);
                    }
                }
            }
        }

        if (cleanupFailures.Count > 0)
        {
            if (testFailure is not null)
            {
                cleanupFailures.Insert(0, testFailure);
            }

            throw new AggregateException(
                testFailure is null
                    ? "Azure Blob cleanup failed for the ephemeral acceptance-test owner."
                    : "The Azure Blob acceptance test and its cleanup failed; see the inner exceptions.",
                cleanupFailures);
        }

        if (testFailure is not null)
        {
            ExceptionDispatchInfo.Capture(testFailure).Throw();
        }
    }

    private static async Task CreateUserAsync(RoamlyApiFactory factory, Guid ownerId, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await users.CreateAsync(new ApplicationUser
        {
            Id = ownerId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAtUtc = TimeProvider.System.GetUtcNow().UtcDateTime,
        }, Password);
        Assert.True(result.Succeeded);
    }

    private static async Task<(Guid Id, string Etag)> CreateTripAsync(
        RoamlyClient client,
        CancellationToken cancellationToken)
    {
        var camperId = Guid.NewGuid();
        using (var camper = await client.PostJsonAsync("/api/v1/campers", new
        {
            id = camperId,
            name = "Synthetic Azure acceptance camper",
            vehicleKind = 1,
        }, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.Created, camper.StatusCode);
        }

        var tripId = Guid.NewGuid();
        using (var trip = await client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/trips",
            new { id = tripId, name = "Synthetic Azure acceptance trip", stops = new[] { new { name = "Test stop" } } },
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.Created, trip.StatusCode);
        }

        using var details = await client.GetAsync($"/api/v1/trips/{tripId:D}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        return (tripId, Assert.IsType<string>(details.Headers.ETag?.ToString()));
    }

    private static async Task<(Guid Id, string Etag)> CreateJournalEntryAsync(
        RoamlyClient client,
        Guid tripId,
        string tripEtag,
        CancellationToken cancellationToken)
    {
        var entryId = Guid.NewGuid();
        using var created = await SendWithEtagAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/trips/{tripId:D}/journal-entries",
            new
            {
                id = entryId,
                entryOnUtc = TimeProvider.System.GetUtcNow().ToString(
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture),
                title = "Synthetic image acceptance entry",
                body = "Synthetic test data only.",
            },
            tripEtag,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (entryId, Assert.IsType<string>(created.Headers.ETag?.ToString()));
    }

    private static async Task<(Guid Id, string Etag)> UploadAttachmentAsync(
        RoamlyClient client,
        Guid tripId,
        Guid entryId,
        string etag,
        CancellationToken cancellationToken)
    {
        using var csrfResponse = await client.GetAsync("/api/v1/csrf-token", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        using var csrfBody = JsonDocument.Parse(
            await csrfResponse.Content.ReadAsStringAsync(cancellationToken));
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}/attachments");
        var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(SyntheticImageBytes), "file", "synthetic.jpg");
        request.Content = multipart;
        request.Headers.Add("X-Roamly-Request", "1");
        request.Headers.Add("X-XSRF-TOKEN", csrfBody.RootElement.GetProperty("token").GetString());
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));

        using var uploaded = await client.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        using var body = JsonDocument.Parse(await uploaded.Content.ReadAsStringAsync(cancellationToken));
        return (
            body.RootElement.GetProperty("id").GetGuid(),
            Assert.IsType<string>(uploaded.Headers.ETag?.ToString()));
    }

    private static async Task DownloadThroughReadOnlySasAsync(
        RoamlyClient client,
        Guid tripId,
        Guid entryId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}/attachments/{attachmentId:D}/download",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var urlText = body.RootElement.GetProperty("url").GetString();
        var validUrl = Uri.TryCreate(urlText, UriKind.Absolute, out var sasUri);
        Assert.True(validUrl);

        using var sasClient = new HttpClient();
        HttpResponseMessage download;
        try
        {
            download = await sasClient.GetAsync(sasUri!, cancellationToken);
        }
        catch
        {
            throw new InvalidOperationException("The SAS download request failed.");
        }

        using (download)
        {
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            var downloadedBytes = await download.Content.ReadAsByteArrayAsync(cancellationToken);
            Assert.True(downloadedBytes.AsSpan().SequenceEqual(SyntheticImageBytes));
        }
    }

    private static async Task AssertExportContainsImageAsync(
        RoamlyClient client,
        Guid entryId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/v1/me/export", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = Assert.Single(
            archive.Entries,
            item => item.FullName == $"attachments/{entryId:D}/{attachmentId:D}");
        await using var image = entry.Open();
        using var contents = new MemoryStream();
        await image.CopyToAsync(contents, cancellationToken);
        Assert.True(contents.ToArray().AsSpan().SequenceEqual(SyntheticImageBytes));
    }

    private static async Task ScheduleImmediateErasureAsync(
        RoamlyApiFactory factory,
        Guid ownerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(ownerId.ToString());
        Assert.NotNull(user);
        user.DeletionRequestedAtUtc = now.AddDays(-31);
        user.DeletionScheduledForUtc = now.AddDays(-1);
        var result = await users.UpdateAsync(user);
        Assert.True(result.Succeeded);
    }

    private static async Task<HttpResponseMessage> SendWithEtagAsync(
        RoamlyClient client,
        HttpMethod method,
        string path,
        object? body,
        string etag,
        CancellationToken cancellationToken)
    {
        using var csrfResponse = await client.GetAsync("/api/v1/csrf-token", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        using var csrfBody = JsonDocument.Parse(
            await csrfResponse.Content.ReadAsStringAsync(cancellationToken));
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add("X-Roamly-Request", "1");
        request.Headers.Add("X-XSRF-TOKEN", csrfBody.RootElement.GetProperty("token").GetString());
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));
        return await client.SendAsync(request, cancellationToken);
    }
}
