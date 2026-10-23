using Azure.Storage.Sas;
using Roamly.Infrastructure.Storage;

namespace Roamly.Model.Tests.Privacy;

public sealed class JournalImageSasPolicyTests
{
    [Fact]
    public void Read_only_download_sas_is_blob_scoped_and_expires_exactly_five_minutes_after_issue_time()
    {
        var issuedAt = new DateTimeOffset(2026, 10, 9, 7, 45, 12, TimeSpan.FromHours(2));

        var sas = JournalImageSasPolicy.CreateReadOnlyBlobSas(
            "private-journal",
            "owner/journal-entry-attachment/entry/attachment",
            issuedAt);

        Assert.Equal("private-journal", sas.BlobContainerName);
        Assert.Equal("owner/journal-entry-attachment/entry/attachment", sas.BlobName);
        Assert.Equal("b", sas.Resource);
        Assert.Equal("r", sas.Permissions);
        Assert.Equal(issuedAt.AddMinutes(5), sas.ExpiresOn);
    }
}
