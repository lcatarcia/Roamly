using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata;
using Roamly.Api.Common.Ownership;
using Roamly.Domain.Abstractions;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Identity;

namespace Roamly.Model.Tests.Privacy;

/// <summary>R9 L0: il piano di sweep deriva il grafo completo delle FK dal modello runtime.</summary>
public sealed class AccountErasurePlanTests(ModelFixture model)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deletion_plan_covers_exactly_runtime_owned_types_in_child_to_parent_order(bool fullSchema)
    {
        var runtimeModel = fullSchema ? model.FullSchemaRuntimeModel : model.ApplicationRuntimeModel;
        var expectedTypes = runtimeModel.GetEntityTypes()
            .Where(entityType => typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType))
            .ToHashSet();
        var plan = AccountErasurePlan.GetDeletionOrder(runtimeModel);

        Assert.Equal(expectedTypes.Count, plan.Count);
        Assert.Equal(expectedTypes, plan.ToHashSet());
        Assert.DoesNotContain(plan, entityType =>
            entityType.ClrType == typeof(ErasureReceipt) || IsIdentityType(entityType.ClrType));

        var position = plan
            .Select((entityType, index) => (entityType, index))
            .ToDictionary(item => item.entityType, item => item.index);
        foreach (var child in expectedTypes)
        {
            foreach (var foreignKey in child.GetForeignKeys())
            {
                if (expectedTypes.Contains(foreignKey.PrincipalEntityType))
                {
                    Assert.True(
                        position[child] < position[foreignKey.PrincipalEntityType],
                        $"{child.ClrType.Name} deve essere eliminato prima di "
                        + $"{foreignKey.PrincipalEntityType.ClrType.Name}.");
                }
            }
        }

        if (fullSchema)
        {
            Assert.Equal(15, plan.Count);
        }
        else
        {
            Assert.Equal(14, plan.Count);
        }
    }

    private static bool IsIdentityType(Type clrType) =>
        clrType.Namespace?.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal) == true
        || typeof(IdentityUser<Guid>).IsAssignableFrom(clrType)
        || typeof(IdentityRole<Guid>).IsAssignableFrom(clrType)
        || clrType == typeof(ApplicationUser);
}
