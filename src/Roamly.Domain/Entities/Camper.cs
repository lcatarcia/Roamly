using Roamly.Domain.Abstractions;
using Roamly.Domain.Enums;
using Roamly.Domain.ValueObjects;

namespace Roamly.Domain.Entities;

/// <summary>
/// Identita' del mezzo: la radice pratica del dominio (CONTEXT.md §2.2).
/// </summary>
public class Camper : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Nome dato dall'utente.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Marca.</summary>
    public string? Brand { get; set; }

    /// <summary>Modello.</summary>
    public string? Model { get; set; }

    /// <summary>Anno di immatricolazione.</summary>
    public short? Year { get; set; }

    /// <summary>Targa: identificativo di un bene registrato, alza la sensibilita' del dataset.</summary>
    public string? PlateNumber { get; set; }

    /// <summary>Tipologia di mezzo.</summary>
    public VehicleKind VehicleKind { get; set; }

    /// <summary>Ingombro, complex type sulla stessa tabella.</summary>
    public Dimensions? Dimensions { get; set; }

    /// <summary>Masse, complex type sulla stessa tabella.</summary>
    public Weights? Weights { get; set; }

    /// <summary>Capacita' di bordo, complex type sulla stessa tabella.</summary>
    public Capacities? Capacities { get; set; }

    /// <summary>Alimentazione.</summary>
    public FuelKind? FuelKind { get; set; }

    /// <summary>Consumo medio dichiarato dall'utente, non calcolato.</summary>
    public decimal? AverageConsumptionLPer100Km { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Token di concorrenza ottimistica.</summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>Accessori installati.</summary>
    public ICollection<Equipment> Equipment { get; } = [];

    /// <summary>Manutenzioni ricorrenti.</summary>
    public ICollection<MaintenanceItem> MaintenanceItems { get; } = [];

    /// <summary>Letture del contachilometri.</summary>
    public ICollection<OdometerReading> OdometerReadings { get; } = [];
}
