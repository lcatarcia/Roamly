using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Roamly.Common.Storage;

namespace Roamly.Infrastructure.Storage;

/// <summary>Adapter Blob per Azurite con shared key locale e Azure con managed identity.</summary>
public sealed class AzureJournalImageStorage : IJournalImageStorage
{
    private readonly BlobServiceClient _service;
    private readonly BlobContainerClient _container;
    private readonly JournalImageStorageSettings _settings;
    private readonly TimeProvider _clock;

    public AzureJournalImageStorage(
        BlobServiceClient service,
        JournalImageStorageSettings settings,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clock);
        _service = service;
        _container = service.GetBlobContainerClient(settings.ContainerName);
        _settings = settings;
        _clock = clock;
    }

    public async Task UploadAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        EnsureCloudAccountWasVerified();
        ArgumentNullException.ThrowIfNull(content);
        await GetBlob(key).UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<JournalImageDownloadLink?> CreateReadOnlyDownloadLinkAsync(
        string key,
        CancellationToken cancellationToken)
    {
        EnsureCloudAccountWasVerified();
        var blob = GetBlob(key);
        if (!(await blob.ExistsAsync(cancellationToken).ConfigureAwait(false)).Value)
        {
            return null;
        }

        var now = _clock.GetUtcNow();
        var builder = JournalImageSasPolicy.CreateReadOnlyBlobSas(
            _container.Name,
            key,
            now);
        var expiresAtUtc = builder.ExpiresOn;

        var uri = blob.CanGenerateSasUri
            ? blob.GenerateSasUri(builder)
            : await CreateDelegatedSasUriAsync(blob, builder, now, expiresAtUtc, cancellationToken)
                .ConfigureAwait(false);
        return new JournalImageDownloadLink(uri, expiresAtUtc);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        EnsureCloudAccountWasVerified();
        try
        {
            return await GetBlob(key).OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteIfExistsAsync(string key, CancellationToken cancellationToken)
    {
        EnsureCloudAccountWasVerified();
        _ = await GetBlob(key).DeleteIfExistsAsync(
            DeleteSnapshotsOption.IncludeSnapshots,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> DeleteByPrefixAsync(string ownerPrefix, CancellationToken cancellationToken)
    {
        EnsureCloudAccountWasVerified();
        long deleted = 0;
        await foreach (var key in ListKeysAsync(ownerPrefix, cancellationToken).ConfigureAwait(false))
        {
            await DeleteIfExistsAsync(key, cancellationToken).ConfigureAwait(false);
            deleted++;
        }

        return deleted;
    }

    public async IAsyncEnumerable<string> ListKeysAsync(
        string prefix,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        EnsureCloudAccountWasVerified();
        await foreach (var blob in _container.GetBlobsAsync(
                BlobTraits.None,
                BlobStates.None,
                prefix,
                cancellationToken).ConfigureAwait(false))
        {
            yield return blob.Name;
        }
    }

    private async Task<Uri> CreateDelegatedSasUriAsync(
        BlobClient blob,
        BlobSasBuilder builder,
        DateTimeOffset now,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var key = await _service.GetUserDelegationKeyAsync(
            now,
            expiresAtUtc,
            cancellationToken).ConfigureAwait(false);
        var query = builder.ToSasQueryParameters(key.Value, _service.AccountName);
        var uri = new UriBuilder(blob.Uri) { Query = query.ToString() };
        return uri.Uri;
    }

    private BlobClient GetBlob(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.StartsWith('/'))
        {
            throw new ArgumentException("La chiave Blob deve essere relativa al container.", nameof(key));
        }

        return _container.GetBlobClient(key);
    }

    private void EnsureCloudAccountWasVerified()
    {
        if (_settings.Provider == "AzureBlob" && !_settings.AzureAccountConfigurationVerified)
        {
            throw new InvalidOperationException(
                "Operazioni Blob cloud disabilitate: verificare soft delete, versioning, snapshot, backup " +
                "e accesso privato dell'account Azure.");
        }
    }
}
