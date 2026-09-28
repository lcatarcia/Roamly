using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Roamly.Infrastructure.Persistence;

/// <summary>
/// Costruisce <see cref="RoamlyDbContext"/> per il <b>tooling di design-time</b>
/// (<c>dotnet ef migrations add</c>, <c>dotnet ef migrations script</c>).
/// <para>
/// ⚠️ <b>Perche' esiste.</b> <see cref="RoamlyDbContext"/> ha un costruttore che esige
/// <c>ICurrentUser</c>, e <c>Program.cs</c> non registra ancora nulla nel contenitore. Senza
/// questa factory <c>dotnet ef</c> ripiegherebbe sull'avvio dell'host dell'API, legando la
/// generazione delle migration al bootstrap di <c>Roamly.Api</c>: un accoppiamento che si
/// paga ogni volta che l'avvio dell'API cambia, per un'operazione che l'API non riguarda.
/// </para>
/// <para>
/// L'utente e' <see cref="NoCurrentUser"/>, lo stesso che usa <c>ModelFixture</c> dei test per la
/// stessa ragione. Non altera l'output: le migration <b>non aprono connessioni</b> e i query
/// filter non producono schema — il filtro "OwnerScope" e' una clausola di query, non un oggetto
/// del database.
/// </para>
/// </summary>
public sealed class RoamlyDbContextFactory : IDesignTimeDbContextFactory<RoamlyDbContext>
{
    /// <summary>
    /// Connection string <b>mai aperta</b>. Il provider serve a far scegliere a EF le annotazioni
    /// SQL Server da cui nasce lo scaffolding della migration, non a raggiungere un server: la
    /// migration si scrive leggendo il modello, non il database. E' la stessa forma — e la stessa
    /// motivazione — di <c>ModelFixture.NeverOpenedConnectionString</c> in <c>Roamly.Model.Tests</c>.
    /// <para>
    /// ⚠️ Se un giorno servisse una connessione vera (per esempio <c>database update</c>), la via
    /// e' <c>dotnet ef --connection "..."</c>, <b>non</b> mettere una stringa reale qui dentro.
    /// </para>
    /// </summary>
    private const string NeverOpenedConnectionString = "Server=none;Database=none;";

    /// <summary>Costruisce il contesto della Phase 1 per il tooling di design-time.</summary>
    /// <param name="args">Argomenti passati dal tooling. Non usati.</param>
    /// <returns>Il contesto su cui si generano le migration.</returns>
    public RoamlyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(NeverOpenedConnectionString)
            .Options;

        return new RoamlyDbContext(options, NoCurrentUser.Instance);
    }
}
