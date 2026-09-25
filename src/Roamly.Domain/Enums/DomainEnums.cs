namespace Roamly.Domain.Enums;

/// <summary>Tipologia di mezzo. Colonna <c>tinyint</c> (CONTEXT.md §2.2).</summary>
public enum VehicleKind : byte
{
    /// <summary>Motorhome integrale.</summary>
    Motorhome = 0,

    /// <summary>Van / camper puro.</summary>
    Van = 1,

    /// <summary>Semintegrale.</summary>
    Coachbuilt = 2,

    /// <summary>Mansardato.</summary>
    Alcove = 3,

    /// <summary>Caravan trainata.</summary>
    Caravan = 4,
}

/// <summary>Alimentazione del mezzo. Colonna <c>tinyint</c>.</summary>
public enum FuelKind : byte
{
    /// <summary>Gasolio.</summary>
    Diesel = 0,

    /// <summary>Benzina.</summary>
    Petrol = 1,

    /// <summary>GPL.</summary>
    Lpg = 2,

    /// <summary>Metano.</summary>
    Cng = 3,

    /// <summary>Elettrico.</summary>
    Electric = 4,

    /// <summary>Ibrido.</summary>
    Hybrid = 5,
}

/// <summary>
/// Categoria di spesa. <b>Non esiste <c>Maintenance</c></b>: il costo dell'intervento vive su
/// <c>MaintenanceLog</c> e contarlo due volte sarebbe un errore di prodotto (CONTEXT.md §2.2).
/// </summary>
public enum ExpenseCategory : byte
{
    /// <summary>Carburante.</summary>
    Fuel = 0,

    /// <summary>Pedaggi.</summary>
    Toll = 1,

    /// <summary>Campeggio o area sosta.</summary>
    Campsite = 2,

    /// <summary>Assicurazione.</summary>
    Insurance = 3,

    /// <summary>Gas.</summary>
    Gas = 4,

    /// <summary>Parcheggio.</summary>
    Parking = 5,

    /// <summary>Altro.</summary>
    Other = 6,
}

/// <summary>Momento a cui una checklist e' legata. Colonna <c>tinyint</c> (CONTEXT.md §2.3).</summary>
public enum ChecklistKind : byte
{
    /// <summary>Partenza.</summary>
    Departure = 0,

    /// <summary>Arrivo.</summary>
    Arrival = 1,

    /// <summary>Rimessaggio.</summary>
    Storage = 2,
}

/// <summary>
/// Provenienza di una riga suggerita dal catalogo. E' un fatto storico e non si riscrive (R24).
/// </summary>
public enum MaintenanceOrigin : byte
{
    /// <summary>Creata dall'utente.</summary>
    User = 0,

    /// <summary>Generata dal catalogo dei seed.</summary>
    Seed = 1,
}

/// <summary>Origine di una lettura del contachilometri. Colonna <c>tinyint</c>.</summary>
public enum OdometerSource : byte
{
    /// <summary>Inserita a mano dall'utente.</summary>
    Manual = 0,

    /// <summary>Derivata dalla registrazione di un intervento.</summary>
    Service = 1,

    /// <summary>Derivata dalla chiusura di un viaggio (Phase 2).</summary>
    TripEnd = 2,
}

/// <summary>Categoria di un luogo salvato. Colonna <c>tinyint</c> (CONTEXT.md §2.3).</summary>
public enum SavedPlaceCategory : byte
{
    /// <summary>Area di sosta.</summary>
    Aire = 0,

    /// <summary>Campeggio.</summary>
    Campsite = 1,

    /// <summary>Parcheggio.</summary>
    Parking = 2,

    /// <summary>Punto panoramico.</summary>
    Viewpoint = 3,

    /// <summary>Ristorante.</summary>
    Restaurant = 4,

    /// <summary>Distributore.</summary>
    FuelStation = 5,

    /// <summary>Officina.</summary>
    Workshop = 6,

    /// <summary>Supermercato.</summary>
    Supermarket = 7,

    /// <summary>Punto di interesse generico.</summary>
    PointOfInterest = 8,
}

/// <summary>
/// Categoria di un accessorio di bordo. Colonna <c>tinyint</c>.
/// I valori derivano dagli esempi di CONTEXT.md §2.2, che non ne fissa l'elenco chiuso.
/// </summary>
public enum EquipmentCategory : byte
{
    /// <summary>Impianto solare.</summary>
    Solar = 0,

    /// <summary>Batteria servizi.</summary>
    Battery = 1,

    /// <summary>Inverter.</summary>
    Inverter = 2,

    /// <summary>Bombole gas.</summary>
    GasBottle = 3,

    /// <summary>Portabici.</summary>
    BikeRack = 4,

    /// <summary>Tendalino.</summary>
    Awning = 5,

    /// <summary>Livellatori.</summary>
    Levellers = 6,

    /// <summary>Altro.</summary>
    Other = 7,
}

/// <summary>
/// Categoria di una manutenzione ricorrente. Colonna <c>tinyint</c>.
/// CONTEXT.md §2.2 dichiara la colonna ma non elenca i valori: vedi rapporto Blocco 2.
/// </summary>
public enum MaintenanceCategory : byte
{
    /// <summary>Motore e trasmissione.</summary>
    Engine = 0,

    /// <summary>Pneumatici.</summary>
    Tyres = 1,

    /// <summary>Freni.</summary>
    Brakes = 2,

    /// <summary>Impianto gas.</summary>
    Gas = 3,

    /// <summary>Impianto idrico.</summary>
    Water = 4,

    /// <summary>Impianto elettrico.</summary>
    Electrical = 5,

    /// <summary>Cellula e carrozzeria.</summary>
    Bodywork = 6,

    /// <summary>Revisione e adempimenti.</summary>
    Inspection = 7,

    /// <summary>Altro.</summary>
    Other = 8,
}

/// <summary>Stati di un viaggio. Quattro valori, matrice di transizione in CONTEXT.md §2.3.</summary>
public enum TripStatus : byte
{
    /// <summary>Pianificato.</summary>
    Planned = 0,

    /// <summary>In corso.</summary>
    Active = 1,

    /// <summary>Concluso.</summary>
    Completed = 2,

    /// <summary>Annullato.</summary>
    Cancelled = 3,
}
