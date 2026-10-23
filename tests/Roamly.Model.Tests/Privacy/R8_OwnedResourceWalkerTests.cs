using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata;
using Roamly.Api.Common.Ownership;
using Roamly.Domain;
using Roamly.Domain.Abstractions;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Model.Tests.Privacy;

/// <summary>R8: il walker dell'export copre solo IOwnedResource dei modelli runtime.</summary>
public sealed class R8_OwnedResourceWalkerTests(ModelFixture model)
{
    private static readonly Type[] ExpectedApplicationManifestTypes =
    [
        typeof(Camper),
        typeof(Equipment),
        typeof(ErasureReceipt),
        typeof(MaintenanceItem),
        typeof(MaintenanceLog),
        typeof(OdometerReading),
        typeof(SavedPlace),
        typeof(Trip),
        typeof(TripStop),
        typeof(Checklist),
        typeof(ChecklistItem),
        typeof(JournalEntry),
        typeof(JournalEntryAttachment),
        typeof(JournalAttachmentDeletionOutboxMessage),
        typeof(Expense),
    ];

    private static readonly Type[] ExpectedApplicationOwnedTypes =
    [
        typeof(Camper),
        typeof(Equipment),
        typeof(MaintenanceItem),
        typeof(MaintenanceLog),
        typeof(OdometerReading),
        typeof(SavedPlace),
        typeof(Trip),
        typeof(TripStop),
        typeof(Checklist),
        typeof(ChecklistItem),
        typeof(JournalEntry),
        typeof(JournalEntryAttachment),
        typeof(JournalAttachmentDeletionOutboxMessage),
        typeof(Expense),
    ];

    [Fact]
    public void Application_walker_matches_the_runtime_model_and_excludes_non_owned_infrastructure()
    {
        var manifestTypes = DomainModelManifest.ApplicationEntityTypes
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
        var expectedManifestTypes = ExpectedApplicationManifestTypes
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedManifestTypes, manifestTypes);

        var runtimeOwnedTypes = model.ApplicationRuntimeModel.GetEntityTypes()
            .Where(entityType => typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => entityType.ClrType)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
        var expectedOwnedTypes = ExpectedApplicationOwnedTypes
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedOwnedTypes, runtimeOwnedTypes);

        var walkedTypes = OwnedResourceWalker.GetEntityTypes(model.ApplicationRuntimeModel)
            .Select(entityType => entityType.ClrType)
            .ToArray();
        Assert.Equal(expectedOwnedTypes, walkedTypes);
        Assert.DoesNotContain(
            OwnedResourceWalker.GetEntityTypes(model.ApplicationRuntimeModel),
            entityType => entityType.ClrType == typeof(ErasureReceipt));
        Assert.DoesNotContain(
            OwnedResourceWalker.GetEntityTypes(model.ApplicationRuntimeModel),
            entityType => entityType.ClrType == typeof(ApplicationUser));
        Assert.DoesNotContain(
            OwnedResourceWalker.GetEntityTypes(model.ApplicationRuntimeModel),
            entityType => entityType.ClrType == typeof(LoginAttemptCounter));
    }

    [Fact]
    public void Full_schema_walker_covers_future_owned_types_without_exporting_identity_or_receipts()
    {
        AssertWalkerMatchesRuntimeModel(model.FullSchemaRuntimeModel);
        var walked = OwnedResourceWalker.GetEntityTypes(model.FullSchemaRuntimeModel);
        Assert.DoesNotContain(walked, entityType => entityType.ClrType == typeof(ErasureReceipt));
        Assert.DoesNotContain(walked, entityType => entityType.ClrType == typeof(LoginAttemptCounter));
        Assert.DoesNotContain(walked, entityType => IsIdentityType(entityType.ClrType));
        Assert.True(walked.Count > OwnedResourceWalker.GetEntityTypes(model.ApplicationRuntimeModel).Count);
    }

    private static void AssertWalkerMatchesRuntimeModel(IModel runtimeModel)
    {
        var expected = runtimeModel.GetEntityTypes()
            .Where(entityType => typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => entityType.ClrType)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
        var actual = OwnedResourceWalker.GetEntityTypes(runtimeModel)
            .Select(entityType => entityType.ClrType)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    private static bool IsIdentityType(Type clrType)
    {
        if (clrType.Namespace?.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal) == true)
        {
            return true;
        }

        for (var current = clrType.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType
                && (current.GetGenericTypeDefinition() == typeof(IdentityUser<>)
                    || current.GetGenericTypeDefinition() == typeof(IdentityRole<>)))
            {
                return true;
            }
        }

        return false;
    }
}
