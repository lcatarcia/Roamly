using Azure.Storage.Sas;

namespace Roamly.Infrastructure.Storage;

internal static class JournalImageSasPolicy
{
    private static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(5);

    public static BlobSasBuilder CreateReadOnlyBlobSas(
        string containerName,
        string blobName,
        DateTimeOffset now)
    {
        var builder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = now.Add(DownloadLifetime),
        };
        builder.SetPermissions(BlobSasPermissions.Read);
        return builder;
    }
}
