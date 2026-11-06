using Roamly.Api.Common.Http;
using Microsoft.EntityFrameworkCore;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Identity.Consents;

/// <summary>Legge lo stato della finalita' server, senza attivare alcuna integrazione.</summary>
[ProtocolOutcomeHandler("404 uniforme per finalita' non esistente.")]
public sealed class GetConsentHandler(ConsentPurposeDefinition definition, RoamlyDbContext db)
{
    public async Task<ConsentStateResponse?> HandleAsync(string purpose, CancellationToken cancellationToken)
    {
        if (purpose != definition.Purpose)
        {
            return null;
        }

        var latest = await db.ConsentTransitions.AsNoTracking()
            .Where(item => item.OwnerId == db.CurrentUserId && item.Purpose == purpose)
            .OrderByDescending(item => item.Revision)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var granted = definition.IsActive && latest?.Action == "grant"
            && latest.Version == definition.Version && latest.TextReference == definition.TextReference;
        return new(definition.Purpose, definition.Version, definition.TextReference,
            definition.IsActive, granted, latest?.Revision ?? 0);
    }
}

/// <summary>Stato derivato, non una proiezione persistente; IsGranted non e' un'autorizzazione Maps.</summary>
public sealed record ConsentStateResponse(
    string Purpose, string Version, string TextReference, bool IsActive, bool IsGranted, long Revision);
