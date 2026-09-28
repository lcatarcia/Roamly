namespace Roamly.IntegrationTests.Infrastructure;

/// <summary>
/// <b>R36 (ADR-0009)</b>: il tag dell'immagine SQL Server, pinnato in un punto unico e versionato.
/// <para>
/// 🔴 Questa costante deve restare l'<b>unica</b> occorrenza del registro nel codice della
/// soluzione: la fixture la usa, il <c>ci.yml</c> del Blocco 5 la leggera' da qui, e
/// <c>Roamly.Benchmarks</c> (R39) la riusera' con risorse diverse. Un tag duplicato diverge in
/// silenzio, e il giorno in cui accade la CI testa un motore diverso da quello locale senza che
/// nulla diventi rosso. Il verificatore e' <c>R36_PinnedSqlServerImageTests</c>, in
/// <c>Roamly.Model.Tests</c>, a L0.
/// </para>
/// <para>
/// Mai <c>latest</c>: un tag mobile rende irriproducibile ogni esito, compreso il rosso.
/// </para>
/// </summary>
public static class SqlServerImage
{
    /// <summary>Immagine e tag pinnati. Unico punto di verita' della versione del motore.</summary>
    public const string Tag = "mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04";
}
