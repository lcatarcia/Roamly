namespace Roamly.Api.Features.Identity.GetMe;

/// <summary>Corpo di risposta di <c>GET /api/v1/me</c>.</summary>
/// <param name="Id">Identificativo utente, radice di ownership.</param>
/// <param name="Email">Email.</param>
/// <param name="DisplayName">Nome visualizzato, se impostato.</param>
/// <param name="ReportingCurrency">Valuta preferita per la presentazione (CONTEXT.md §2.1: non converte nulla).</param>
public sealed record GetMeResponse(Guid Id, string Email, string? DisplayName, string ReportingCurrency);
