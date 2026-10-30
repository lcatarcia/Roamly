using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Checklists.PreviewChecklistSeed;

internal static class ChecklistSeedCatalog
{
    public const string Version = "1";

    public static IReadOnlyList<ChecklistSeedPreviewItemResponse> Build(
        ChecklistKind kind,
        DateOnly? plannedStartOnUtc,
        DateOnly? plannedEndOnUtc,
        IReadOnlySet<EquipmentCategory> equipment)
    {
        var items = kind switch
        {
            ChecklistKind.Departure => DepartureItems(),
            ChecklistKind.Arrival => ArrivalItems(),
            ChecklistKind.Storage => StorageItems(),
            _ => [],
        };

        if (HasAtLeastSevenNights(plannedStartOnUtc, plannedEndOnUtc)
            && kind == ChecklistKind.Departure)
        {
            items.Add(new("duration.water-gas", "Pianifica i rifornimenti di acqua e gas per la durata del viaggio"));
        }

        AddEquipmentItems(kind, equipment, items);
        return items.DistinctBy(item => item.Text, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string NameFor(ChecklistKind kind) => kind switch
    {
        ChecklistKind.Departure => "Partenza",
        ChecklistKind.Arrival => "Arrivo",
        ChecklistKind.Storage => "Rimessaggio",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string? TextForKey(string templateKey) => templateKey switch
    {
        "duration.water-gas" => "Pianifica i rifornimenti di acqua e gas per la durata del viaggio",
        "equipment.bike-rack" => "Blocca il carico sul portabici",
        "equipment.awning" => "Chiudi e blocca il tendalino",
        "equipment.levellers" => "Prepara i livellatori",
        "equipment.battery" => "Verifica la batteria servizi",
        _ => AllItems().FirstOrDefault(item => item.TemplateKey == templateKey)?.Text,
    };

    private static List<ChecklistSeedPreviewItemResponse> DepartureItems() =>
    [
        new("departure.windows", "chiudi finestre"),
        new("departure.gas", "chiudi gas"),
        new("departure.power", "scollega corrente"),
        new("departure.water", "controlla acqua"),
        new("departure.tyres", "controlla gomme"),
        new("departure.lights", "controlla luci"),
        new("departure.interior-load", "blocca oggetti interni"),
    ];

    private static List<ChecklistSeedPreviewItemResponse> ArrivalItems() =>
    [
        new("arrival.level-camper", "livella camper"),
        new("arrival.power", "collega corrente"),
        new("arrival.water", "controlla acqua"),
        new("arrival.outdoor-area", "configura area esterna"),
    ];

    private static List<ChecklistSeedPreviewItemResponse> StorageItems() =>
    [
        new("storage.water", "scarica acqua"),
        new("storage.battery", "controlla batteria"),
        new("storage.gas", "chiudi gas"),
        new("storage.fridge", "controlla frigorifero"),
        new("storage.protection", "proteggi il mezzo"),
    ];

    private static IEnumerable<ChecklistSeedPreviewItemResponse> AllItems()
    {
        foreach (var kind in Enum.GetValues<ChecklistKind>())
        {
            foreach (var item in Build(kind, null, null, new HashSet<EquipmentCategory>()))
            {
                yield return item;
            }
        }
    }

    private static bool HasAtLeastSevenNights(DateOnly? start, DateOnly? end) =>
        start.HasValue && end.HasValue && end.Value.DayNumber - start.Value.DayNumber >= 7;

    private static void AddEquipmentItems(
        ChecklistKind kind,
        IReadOnlySet<EquipmentCategory> equipment,
        List<ChecklistSeedPreviewItemResponse> items)
    {
        if (kind == ChecklistKind.Departure && equipment.Contains(EquipmentCategory.BikeRack))
        {
            items.Add(new("equipment.bike-rack", "Blocca il carico sul portabici"));
        }

        if (kind == ChecklistKind.Departure && equipment.Contains(EquipmentCategory.Awning))
        {
            items.Add(new("equipment.awning", "Chiudi e blocca il tendalino"));
        }

        if (kind == ChecklistKind.Arrival && equipment.Contains(EquipmentCategory.Levellers))
        {
            items.Add(new("equipment.levellers", "Prepara i livellatori"));
        }

        if (kind == ChecklistKind.Storage && equipment.Contains(EquipmentCategory.Battery))
        {
            items.Add(new("equipment.battery", "Verifica la batteria servizi"));
        }
    }
}
