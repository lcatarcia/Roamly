using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Checklists.PreviewChecklistSeed;

/// <summary>Anteprima read-only di una lista generata dal catalogo controllato.</summary>
public sealed record ChecklistSeedPreviewResponse(
    string Name,
    ChecklistKind Kind,
    string CatalogVersion,
    IReadOnlyList<ChecklistSeedPreviewItemResponse> Items);

/// <summary>Voce proposta, identificata dal suo template versionato.</summary>
public sealed record ChecklistSeedPreviewItemResponse(string TemplateKey, string Text);
