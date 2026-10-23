namespace Roamly.Api.Common.Http;

/// <summary>Pagina keyset di una collection API (R52).</summary>
/// <typeparam name="T">Tipo del read model contenuto.</typeparam>
/// <param name="Items">Elementi della pagina.</param>
/// <param name="NextCursor">Cursore opaco per la pagina successiva, nullo alla fine.</param>
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
