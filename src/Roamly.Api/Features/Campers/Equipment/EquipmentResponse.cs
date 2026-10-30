namespace Roamly.Api.Features.Campers.Equipment;

/// <summary>Rappresentazione HTTP dell'accessorio senza owner o token persistente.</summary>
public sealed record EquipmentResponse(
    Guid Id,
    string Name,
    int Category,
    string? Notes,
    DateOnly? InstalledOnUtc)
{
    /// <summary>Proietta i soli campi visibili dell'accessorio.</summary>
    public static EquipmentResponse From(Roamly.Domain.Entities.Equipment equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        return new EquipmentResponse(
            equipment.Id,
            equipment.Name,
            (int)equipment.Category,
            equipment.Notes,
            equipment.InstalledOnUtc);
    }
}
