using Microsoft.EntityFrameworkCore;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Common.Ownership;

/// <summary>
/// Letture dei viaggi su SQL Server che coordinano l'associazione delle spese con le transizioni di stato.
/// </summary>
internal static class TripAssociationLockQueries
{
    // I chiamanti serializzabili mantengono il lock di aggiornamento fino al salvataggio/commit;
    // UPDLOCK evita gare durante la conversione dei lock di lettura condivisi.
    public static Task<Trip?> FindForExpenseAssociationAsync(
        RoamlyDbContext db,
        Guid tripId,
        Guid camperId,
        CancellationToken cancellationToken)
    {
        return db.Trips.FromSqlInterpolated($"""
                SELECT [trip].*,
                    [trip].[FuelPricePerLiterAmount] AS [FuelPricePerLiter_Amount],
                    [trip].[FuelPricePerLiterCurrency] AS [FuelPricePerLiter_Currency]
                FROM [Trips] AS [trip] WITH (UPDLOCK, HOLDLOCK)
                WHERE [trip].[OwnerId] = {db.CurrentUserId}
                    AND [trip].[Id] = {tripId}
                    AND [trip].[CamperId] = {camperId}
                """)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public static Task<Trip?> FindForTransitionAsync(
        RoamlyDbContext db,
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return db.Trips.FromSqlInterpolated($"""
                SELECT [trip].*,
                    [trip].[FuelPricePerLiterAmount] AS [FuelPricePerLiter_Amount],
                    [trip].[FuelPricePerLiterCurrency] AS [FuelPricePerLiter_Currency]
                FROM [Trips] AS [trip] WITH (UPDLOCK, HOLDLOCK)
                WHERE [trip].[OwnerId] = {db.CurrentUserId} AND [trip].[Id] = {tripId}
                """)
            .Include(trip => trip.Stops)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
