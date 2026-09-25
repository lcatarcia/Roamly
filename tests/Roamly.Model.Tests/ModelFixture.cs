using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Roamly.Domain.Abstractions;
using Roamly.Infrastructure.Persistence;

[assembly: AssemblyFixture(typeof(Roamly.Model.Tests.ModelFixture))]

namespace Roamly.Model.Tests;

/// <summary>
/// Costruisce <b>una sola volta per assembly</b> i modelli EF dei due contesti e li espone ai
/// verificatori L0. Nessuna connessione viene mai aperta (R30, ADR-0009): la connection string
/// e' finta e serve solo a far scegliere a EF il provider SQL Server, che e' cio' che determina
/// le annotazioni del modello.
/// </summary>
public sealed class ModelFixture
{
    /// <summary>
    /// Connection string mai aperta. Il provider serve per le annotazioni, non per il traffico:
    /// SQL Server vero arriva al Blocco 4 (L1), non qui.
    /// </summary>
    private const string NeverOpenedConnectionString = "Server=none;Database=none;";

    /// <summary>Costruisce i modelli offline.</summary>
    public ModelFixture()
    {
        FullSchemaModel = BuildFullSchemaModel();
        Phase1Model = BuildPhase1Model();

        OwnedEntityTypes = [.. FullSchemaModel.GetEntityTypes()
            .Where(IsOwned)
            .OrderBy(e => e.ClrType.Name, StringComparer.Ordinal)];
    }

    /// <summary>Modello di <see cref="FullSchemaDbContext"/>: tutte e 14 le entita' piu' Identity.</summary>
    public IModel FullSchemaModel { get; }

    /// <summary>Modello di <see cref="RoamlyDbContext"/>: solo le sei entita' della Phase 1, piu' Identity.</summary>
    public IModel Phase1Model { get; }

    /// <summary>Entity type dello schema completo che implementano <see cref="IOwnedResource"/>.</summary>
    public IReadOnlyList<IEntityType> OwnedEntityTypes { get; }

    /// <summary>Vero se l'entity type e' una risorsa owned (esclude Identity ed <c>ErasureReceipt</c>).</summary>
    /// <param name="entityType">Entity type da classificare.</param>
    /// <returns><see langword="true"/> se il tipo CLR implementa <see cref="IOwnedResource"/>.</returns>
    public static bool IsOwned(IEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType);
    }

    /// <summary>Nome della tabella dell'entity type, con il nome CLR come fallback.</summary>
    /// <param name="entityType">Entity type da nominare.</param>
    /// <returns>Nome leggibile nei messaggi di fallimento.</returns>
    public static string TableName(IEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return entityType.GetTableName() ?? entityType.ClrType.Name;
    }

    private static IModel BuildFullSchemaModel()
    {
        var options = new DbContextOptionsBuilder<FullSchemaDbContext>()
            .UseSqlServer(NeverOpenedConnectionString)
            .Options;

        using var context = new FullSchemaDbContext(options, NoCurrentUser.Instance);
        return DesignTimeModelOf(context);
    }

    private static IModel BuildPhase1Model()
    {
        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(NeverOpenedConnectionString)
            .Options;

        using var context = new RoamlyDbContext(options, NoCurrentUser.Instance);
        return DesignTimeModelOf(context);
    }

    /// <summary>
    /// 🔴 Il modello si legge da <see cref="IDesignTimeModel"/>, <b>mai</b> da <c>DbContext.Model</c>
    /// (TESTING.md §8.2). <c>DbContext.Model</c> e' il modello read-optimized di runtime: EF ne rimuove
    /// le annotazioni che servono solo alla generazione dello schema, fra cui <c>SqlServer:Clustered</c>.
    /// Su quel modello <c>IsClustered()</c> lancia e <c>FindAnnotation("SqlServer:Clustered")</c>
    /// restituisce <see langword="null"/> su tutte le entita': un verificatore di R26 scritto in forma
    /// permissiva sarebbe verde su tutto, con il controllo mai in vigore.
    /// Il tipo vive in <c>Microsoft.EntityFrameworkCore.Metadata</c>, non in <c>...Infrastructure</c>.
    /// </summary>
    private static IModel DesignTimeModelOf(DbContext context)
        => context.GetService<IDesignTimeModel>().Model;
}
