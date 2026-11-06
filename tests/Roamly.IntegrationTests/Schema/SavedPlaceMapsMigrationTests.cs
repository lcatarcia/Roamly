using Microsoft.EntityFrameworkCore;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Schema;

[Collection(DatabaseCollection.Name)]
public sealed class SavedPlaceMapsMigrationTests(SqlServerFixture sqlServer)
{
    private const string MainTarget = "20261104093558_AddSavedPlaceRowVersion";
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task maps_migrations_upgrade_main_without_changing_saved_place_or_rowversion()
    {
        await using var database = await sqlServer.CreateEmptyDatabaseAsync("Maps upgrade from main");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync(MainTarget, Cancellation);
        var ownerId = Guid.NewGuid();
        var placeId = Guid.NewGuid();
        await InsertMainDataAsync(context, ownerId, placeId);
        var rowVersion = await ReadRowVersionAsync(context, placeId);
        await context.Database.MigrateAsync(Cancellation);
        await AssertSavedPlaceAsync(context, placeId, rowVersion);
        await AssertMigrationHistoryAsync(context);
    }

    [Fact]
    public async Task combined_migrations_apply_to_empty_sql_and_match_current_model()
    {
        var database = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(database.Failure);
        await using var context = CreateContext(database.ConnectionString);
        await AssertMigrationHistoryAsync(context);
        var columns = await SchemaCatalog.ColumnTypesAsync(database.ConnectionString);
        Assert.Contains(columns, column => column.TableName == "SavedPlaces"
            && column.ColumnName == "RowVersion" && column.TypeName is "timestamp" or "rowversion");
        Assert.Contains(columns, column => column.TableName == "SavedPlaces" && column.ColumnName == "GoogleMapsUrl");
        Assert.Contains(columns, column => column.TableName == "ConsentTransitions");
        Assert.Contains(columns, column => column.TableName == "MapsEconomicStates");
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static RoamlyDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<RoamlyDbContext>().UseSqlServer(connectionString).Options;
        return new RoamlyDbContext(options, ThrowingCurrentUser.Instance);
    }

    private static async Task InsertMainDataAsync(RoamlyDbContext context, Guid ownerId, Guid placeId)
    {
        var created = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [AspNetUsers]
                ([Id], [EmailConfirmed], [PhoneNumberConfirmed], [TwoFactorEnabled],
                 [LockoutEnabled], [AccessFailedCount], [CreatedAtUtc], [RequiresPasswordChange])
            VALUES ({ownerId}, 1, 0, 0, 0, 0, {created}, 0);
            INSERT INTO [SavedPlaces] ([OwnerId], [Id], [Name], [Category], [GoogleMapsUrl], [CreatedAtUtc])
            VALUES ({ownerId}, {placeId}, N'Luogo da main', 10, N'https://maps.google.com/?q=main', {created});
            """, Cancellation);
    }

    private static Task<byte[]> ReadRowVersionAsync(RoamlyDbContext context, Guid placeId)
    {
        return context.Database.SqlQuery<byte[]>(
            $"SELECT CONVERT(varbinary(8), [RowVersion]) AS [Value] FROM [SavedPlaces] WHERE [Id] = {placeId}")
            .SingleAsync(Cancellation);
    }

    private static async Task AssertSavedPlaceAsync(RoamlyDbContext context, Guid placeId, byte[] rowVersion)
    {
        Assert.Equal(rowVersion, await ReadRowVersionAsync(context, placeId));
        Assert.Equal("Luogo da main", await context.Database.SqlQuery<string>(
            $"SELECT [Name] AS [Value] FROM [SavedPlaces] WHERE [Id] = {placeId}").SingleAsync(Cancellation));
        Assert.Equal("https://maps.google.com/?q=main", await context.Database.SqlQuery<string>(
            $"SELECT [GoogleMapsUrl] AS [Value] FROM [SavedPlaces] WHERE [Id] = {placeId}").SingleAsync(Cancellation));
    }

    private static async Task AssertMigrationHistoryAsync(RoamlyDbContext context)
    {
        var applied = (await context.Database.GetAppliedMigrationsAsync(Cancellation)).ToArray();
        Assert.Equal(context.Database.GetMigrations().ToArray(), applied);
        Assert.Contains(MainTarget, applied);
        Assert.Contains("20261106091448_AddIdentityConsentRegistry", applied);
        Assert.Contains("20261106105835_AddMapsEconomicGate", applied);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(Cancellation));
    }
}
