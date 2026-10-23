using Roamly.Api.Features.Authentication;
using Roamly.Api.Features.Campers;
using Roamly.Api.Features.Campers.Equipment;
using Roamly.Api.Features.Identity;
using Roamly.Api.Features.Trips;
using Roamly.Api.Features.Checklists;
using Roamly.Api.Features.Expenses;
using Roamly.Api.Features.Journal;

namespace Roamly.Api.Common.Startup;

/// <summary>Composition root dei moduli HTTP di Roamly, condiviso con i verificatori degli endpoint.</summary>
public static class RoamlyEndpointRegistration
{
    /// <summary>Registra tutti gli endpoint applicativi.</summary>
    /// <param name="app">Route builder dell'applicazione.</param>
    /// <returns>Il route builder, per concatenare la pipeline.</returns>
    public static IEndpointRouteBuilder MapRoamlyEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapAuthenticationEndpoints();
        app.MapIdentityEndpoints();
        app.MapCamperEndpoints();
        app.MapEquipmentEndpoints();
        app.MapTripEndpoints();
        app.MapChecklistEndpoints();
        app.MapExpenseEndpoints();
        app.MapJournalEndpoints();

        return app;
    }
}
