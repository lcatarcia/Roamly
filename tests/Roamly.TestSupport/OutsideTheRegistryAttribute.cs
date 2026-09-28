namespace Roamly.TestSupport;

/// <summary>
/// Marca un builder che <b>non</b> fa parte del registro coperto da R33 (ADR-0009).
/// <para>
/// Il caso previsto e' <c>ErasureReceipt</c>: non implementa <c>IOwnedResource</c> per costruzione
/// (R10, ADR-0004 — non deve avere alcuna FK verso <c>AspNetUsers</c>), quindi R33 copre
/// <b>13 tipi su 14</b>. Un builder per il test di idempotenza e' legittimo, ma vive fuori dal
/// registro: senza questo attributo, aggiungerlo per distrazione farebbe fallire R33 con il
/// messaggio sbagliato — "builder in piu'" invece di "entita' non owned".
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OutsideTheRegistryAttribute : Attribute
{
    /// <summary>Costruisce l'attributo dichiarando la ragione dell'esclusione.</summary>
    /// <param name="reason">Perche' il tipo sta fuori dal registro.</param>
    public OutsideTheRegistryAttribute(string reason) => Reason = reason;

    /// <summary>Ragione dell'esclusione, leggibile in un messaggio di fallimento.</summary>
    public string Reason { get; }
}
