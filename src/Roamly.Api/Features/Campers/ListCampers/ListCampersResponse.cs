using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Campers.ListCampers;

/// <summary>Voce essenziale del camper per la selezione.</summary>
/// <param name="Id">Identificativo del camper.</param>
/// <param name="Name">Nome scelto dall'utente.</param>
/// <param name="VehicleKind">Tipologia del mezzo.</param>
public sealed record ListCampersResponse(Guid Id, string Name, VehicleKind VehicleKind);
