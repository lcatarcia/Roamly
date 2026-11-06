namespace Roamly.Api.Features.Identity.Consents;

/// <summary>Definizione server immutabile: il composition root seleziona draft/v1 dalla configurazione host.</summary>
public sealed record ConsentPurposeDefinition(string Purpose, string Version, string TextReference, bool IsActive)
{
    /// <summary>Segnaposto non legale: non autorizza raccolta effettiva, token o rendering.</summary>
    public static ConsentPurposeDefinition AzureMapsDraft { get; } =
        new("azure-maps-render", "DRAFT-1", "DRAFT:azure-maps-render:DRAFT-1", false);

    /// <summary>Descrizione tecnica v1, non certificazione legale. Attiva solo con Maps configurato.</summary>
    public static ConsentPurposeDefinition AzureMapsV1 { get; } =
        new("azure-maps-render", "v1", "roamly:azure-maps-render:v1", true);
}
