namespace Roamly.IntegrationTests.Infrastructure;

/// <summary>
/// Isolamento <b>G1</b> di ADR-0009: un solo database, tutti i test che lo toccano in questa
/// collection, Respawn tra un test e l'altro.
/// <para>
/// Esiste dal giorno 1 proprio per rendere economico il passaggio a <b>G2</b> (un database per
/// collection) quando il trigger T1 scattera': cambia <b>dove punta la lease</b>, non i test.
/// Costo di inversione dichiarato: ~15 righe di <see cref="DatabaseLease"/>.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Il suffisso Collection e' la convenzione di xUnit per le collection definition: "
        + "rinominarlo renderebbe il file irriconoscibile a chi cerca il punto di sblocco G1 → G2.")]
public sealed class DatabaseCollection
{
    /// <summary>Nome della collection, unico in G1.</summary>
    public const string Name = "Database";
}
