using Roamly.Domain.Entities;

namespace Roamly.Model.Tests.Schema;

public sealed class JournalModelActivationTests(ModelFixture model)
{
    [Fact]
    public void The_application_model_includes_journal_entries()
    {
        Assert.Contains(model.ApplicationModel.GetEntityTypes(), entityType =>
            entityType.ClrType == typeof(JournalEntry));
        Assert.Contains(model.ApplicationModel.GetEntityTypes(), entityType =>
            entityType.ClrType == typeof(JournalEntryAttachment));
        Assert.Contains(model.ApplicationModel.GetEntityTypes(), entityType =>
            entityType.ClrType == typeof(JournalAttachmentDeletionOutboxMessage));
    }
}
