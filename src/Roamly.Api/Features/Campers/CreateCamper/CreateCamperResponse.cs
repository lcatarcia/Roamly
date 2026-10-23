using Roamly.Domain.Entities;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Campers.CreateCamper;

/// <summary>Read model restituito dopo la creazione o il replay.</summary>
/// <param name="Id">Identificativo inviato dal client.</param>
/// <param name="Name">Nome normalizzato del camper.</param>
/// <param name="VehicleKind">Tipologia del mezzo.</param>
public sealed record CreateCamperResponse(Guid Id, string Name, VehicleKind VehicleKind)
{
    /// <summary>Crea il read model dalla riga persistita.</summary>
    public static CreateCamperResponse From(Camper camper)
    {
        ArgumentNullException.ThrowIfNull(camper);
        return new CreateCamperResponse(camper.Id, camper.Name, camper.VehicleKind);
    }
}
