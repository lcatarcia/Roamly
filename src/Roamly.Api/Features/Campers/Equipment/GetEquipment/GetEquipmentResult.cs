using Roamly.Api.Features.Campers.Equipment;

namespace Roamly.Api.Features.Campers.Equipment.GetEquipment;

/// <summary>Dettaglio dell'accessorio e rowversion da esporre come ETag.</summary>
public sealed record GetEquipmentResult(EquipmentResponse Equipment, byte[] RowVersion);
