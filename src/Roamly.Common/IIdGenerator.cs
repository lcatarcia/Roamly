namespace Roamly.Common;

/// <summary>
/// Unica sorgente ammessa per il valore di <c>Id</c> di un'entita' owned (R41, ADR-0008).
/// </summary>
public interface IIdGenerator
{
    /// <summary>Produce un nuovo identificativo.</summary>
    Guid NewId();
}
