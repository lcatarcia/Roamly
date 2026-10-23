using Azure;
using Azure.Storage.Blobs;
using Roamly.Infrastructure.Storage;

namespace Roamly.Model.Tests.Privacy;

public sealed class JournalStorageVerificationTests
{
    [Fact]
    public async Task Unverified_Azure_configuration_blocks_signed_downloads_and_export_reads()
    {
        var options = new BlobClientOptions();
        options.Retry.MaxRetries = 0;
        var service = new BlobServiceClient(
            new Uri("http://127.0.0.1:1"),
            new AzureSasCredential("unused"),
            options);
        var settings = new JournalImageStorageSettings(
            "AzureBlob",
            "private-journal",
            null,
            new Uri("https://account.blob.core.windows.net"),
            AzureAccountConfigurationVerified: false);
        var storage = new AzureJournalImageStorage(service, settings, TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.CreateReadOnlyDownloadLinkAsync(
                "owner/journal-entry-attachment/entry/image",
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.OpenReadAsync(
                "owner/journal-entry-attachment/entry/image",
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in storage.ListKeysAsync(
                "owner/",
                TestContext.Current.CancellationToken))
            {
            }
        });
    }
}
