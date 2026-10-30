internal static class BootstrapMigrationHistory
{
    internal static IReadOnlyList<string> ExpectedMigrationIds { get; } = Array.AsReadOnly<string>(
    [
        "20260928095239_InitialPhase1",
        "20261005153020_AddEquipmentRowVersion",
        "20261006121343_AddDataProtectionKeys",
        "20261006144508_AddLoginAttemptCounters",
        "20261006160121_AddLoginCounterKinds",
        "20261007103208_AddTripsAndTripStops",
        "20261008101448_ActivateExpenseManagement",
        "20261008140401_ActivateTripChecklists",
        "20261009073211_ActivateJournalEntries",
        "20261009083233_AddJournalAttachmentDeletionOutbox",
        "20261009131921_AddTripRouteEstimates",
        "20261021105543_AddTripFuelPrice",
        "20261021125327_EnforceTripFuelPricePair",
        "20261022145640_RequirePasswordChangeForProvisionedUsers",
        "20261028134346_AddCamperManualSpecifications"
    ]);

    internal static MigrationHistoryDifference Compare(
        IEnumerable<string> applied,
        IEnumerable<string> expected)
    {
        var actualIds = new HashSet<string>(applied, StringComparer.Ordinal);
        var expectedIds = new HashSet<string>(expected, StringComparer.Ordinal);

        string[] missing = expectedIds.Except(actualIds, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] unexpected = actualIds.Except(expectedIds, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new MigrationHistoryDifference(missing, unexpected);
    }
}

internal sealed record MigrationHistoryDifference(
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Unexpected)
{
    internal bool IsExact => Missing.Count == 0 && Unexpected.Count == 0;
}
