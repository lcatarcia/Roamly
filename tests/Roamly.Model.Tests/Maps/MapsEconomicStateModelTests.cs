using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Ownership;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Model.Tests.Maps;

public sealed class MapsEconomicStateModelTests(ModelFixture model)
{
    [Fact]
    public void Global_resource_state_has_no_personal_owner_and_is_not_exported_or_erased()
    {
        var entity = model.ApplicationModel.FindEntityType(typeof(MapsEconomicState));
        Assert.NotNull(entity);
        Assert.Null(entity.FindProperty("OwnerId"));
        Assert.Empty(entity.GetForeignKeys());
        Assert.Empty(entity.GetDeclaredQueryFilters());
        Assert.Equal(["ResourceId"], entity.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.NotEmpty(entity.GetCheckConstraints());
        Assert.DoesNotContain(OwnedResourceWalker.GetEntityTypes(model.ApplicationRuntimeModel),
            item => item.ClrType == typeof(MapsEconomicState));
    }
}
