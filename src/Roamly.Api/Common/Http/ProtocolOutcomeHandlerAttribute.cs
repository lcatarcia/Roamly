namespace Roamly.Api.Common.Http;

/// <summary>
/// Dichiara che l'handler modella esiti di protocollo, anziche' rifiuti di dominio (R47).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ProtocolOutcomeHandlerAttribute(string reason) : Attribute
{
    /// <summary>Motivo per cui gli esiti dell'handler seguono un contratto di protocollo esplicito.</summary>
    public string Reason { get; } = reason;
}
