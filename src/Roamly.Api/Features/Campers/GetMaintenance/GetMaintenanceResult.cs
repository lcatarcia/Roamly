namespace Roamly.Api.Features.Campers.GetMaintenance;

/// <summary>Dettaglio della manutenzione e rowversion da esporre come ETag.</summary>
public sealed record GetMaintenanceResult(MaintenanceDetailResponse Maintenance, byte[] RowVersion);
