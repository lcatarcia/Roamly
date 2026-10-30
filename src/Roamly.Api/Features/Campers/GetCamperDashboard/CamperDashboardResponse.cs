namespace Roamly.Api.Features.Campers.GetCamperDashboard;

/// <summary>Proiezione della dashboard di un singolo camper.</summary>
public sealed record CamperDashboardResponse(
    CamperDashboardCamper Camper,
    CamperDashboardOdometer? Odometer,
    IReadOnlyList<CamperDashboardMaintenance> Maintenance);

/// <summary>Anagrafica utile a identificare il mezzo.</summary>
public sealed record CamperDashboardCamper(
    Guid Id,
    string Name,
    string? Brand,
    string? Model,
    short? Year,
    int VehicleKind);

/// <summary>Lettura datata del contachilometri, qualificata rispetto ai 90 giorni.</summary>
public sealed record CamperDashboardOdometer(int ReadingKm, DateOnly TakenOnUtc, bool IsStale);

/// <summary>Una manutenzione attiva con i due assi di scadenza indipendenti.</summary>
public sealed record CamperDashboardMaintenance(
    Guid Id,
    string Name,
    bool HasServiceHistory,
    DateOnly? LastServiceOnUtc,
    CamperDashboardTimeAxis TimeAxis,
    CamperDashboardOdometerAxis OdometerAxis);

/// <summary>Stato della sola soglia temporale.</summary>
public sealed record CamperDashboardTimeAxis(string Status, DateOnly? DueOnUtc);

/// <summary>Stato della sola soglia chilometrica.</summary>
public sealed record CamperDashboardOdometerAxis(string Status, int? DueAtKm, int? KmRemaining);

/// <summary>Valori stabili degli stati esposti per ciascun asse.</summary>
public static class CamperDashboardAxisStatus
{
    /// <summary>Non esistono ancora interventi da cui ricavare una scadenza.</summary>
    public const string NotScheduled = "NotScheduled";
    /// <summary>L'item non ha una ricorrenza su questo asse.</summary>
    public const string NotConfigured = "NotConfigured";
    /// <summary>I dati disponibili non consentono una verifica attendibile.</summary>
    public const string Unknown = "Unknown";
    /// <summary>La soglia è oltre la finestra di attenzione.</summary>
    public const string Ok = "Ok";
    /// <summary>La soglia è entro 30 giorni oppure 1.000 km.</summary>
    public const string DueSoon = "DueSoon";
    /// <summary>La soglia è stata raggiunta o superata.</summary>
    public const string Overdue = "Overdue";
}
