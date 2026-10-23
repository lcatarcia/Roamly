using Roamly.Api.Features.Journal;

namespace Roamly.Model.Tests.Privacy;

public sealed class JournalAttachmentReconciliationPlanTests
{
    [Fact]
    public void Reconciliation_reports_orphan_blobs_and_dangling_rows_in_both_directions()
    {
        var report = JournalAttachmentReconciliationPlan.Compare(
            ["owner/attachment/present", "owner/attachment/orphan"],
            ["owner/attachment/present", "owner/attachment/missing"]);

        Assert.Equal(1, report.OrphanBlobCount);
        Assert.Equal(1, report.DanglingSqlRowCount);
    }

    [Fact]
    public void Reconciliation_treats_matching_keys_as_clean_and_duplicates_as_one_object()
    {
        var report = JournalAttachmentReconciliationPlan.Compare(
            ["owner/attachment/a", "owner/attachment/a"],
            ["owner/attachment/a"]);

        Assert.Equal(new JournalAttachmentReconciliationReport(0, 0), report);
    }
}
