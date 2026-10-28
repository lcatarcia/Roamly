using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Roamly.Infrastructure.Persistence;

namespace Roamly.RuntimeBootstrap.Tests;

public sealed class BootstrapMigrationHistoryTests
{
    [Fact]
    public void Exact_applied_migration_set_is_accepted()
    {
        string[] expected = ["20260101000000_Initial"];

        MigrationHistoryDifference difference =
            BootstrapMigrationHistory.Compare(expected, expected);

        Assert.True(difference.IsExact);
        Assert.Empty(difference.Missing);
        Assert.Empty(difference.Unexpected);
    }

    [Fact]
    public void Missing_and_unexpected_migrations_are_reported()
    {
        MigrationHistoryDifference difference = BootstrapMigrationHistory.Compare(
            ["20260101000000_Initial", "20260103000000_Unexpected"],
            ["20260101000000_Initial", "20260102000000_Expected"]);

        Assert.False(difference.IsExact);
        Assert.Equal(["20260102000000_Expected"], difference.Missing);
        Assert.Equal(["20260103000000_Unexpected"], difference.Unexpected);
    }

    [Fact]
    public void Runtime_bootstrap_migration_ids_match_the_application_migrations()
    {
        using RoamlyDbContext context = new RoamlyDbContextFactory().CreateDbContext([]);
        IEnumerable<string> applicationMigrations = context
            .GetService<IMigrationsAssembly>()
            .Migrations.Keys;

        MigrationHistoryDifference difference = BootstrapMigrationHistory.Compare(
            applicationMigrations,
            BootstrapMigrationHistory.ExpectedMigrationIds);

        Assert.True(
            difference.IsExact,
            "Migration helper/application mismatch. Missing: "
            + string.Join(", ", difference.Missing)
            + "; unexpected: "
            + string.Join(", ", difference.Unexpected));
    }
}
