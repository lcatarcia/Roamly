using Microsoft.EntityFrameworkCore;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Model.Tests.Ownership;

/// <summary>R56: i contatori di sicurezza non entrano nel grafo owner-scoped o privacy.</summary>
public sealed class LoginAttemptCounterModelTests(ModelFixture model)
{
    [Fact]
    public void Counter_is_infrastructure_only_without_owner_filter_or_identity_foreign_key()
    {
        var entity = model.ApplicationModel.FindEntityType(typeof(LoginAttemptCounter));

        Assert.NotNull(entity);
        Assert.Equal("LoginAttemptCounters", entity.GetTableName());
        Assert.Null(entity.FindProperty("OwnerId"));
        Assert.Empty(entity.GetForeignKeys());
        Assert.Empty(entity.GetDeclaredQueryFilters());
        Assert.Equal("char(64)", entity.FindProperty(nameof(LoginAttemptCounter.BucketKey))?.GetColumnType());
    }
}
