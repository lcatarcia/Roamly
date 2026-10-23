using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Features.Campers.GetCamperProfile;
using Roamly.Domain.ValueObjects;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.UpdateCamperProfile;

/// <summary>Aggiorna il profilo completo con ownership e concorrenza ottimistica.</summary>
[ProtocolOutcomeHandler("404, 428 e 412 sono esiti HTTP distinti previsti da ADR-0005.")]
public sealed class UpdateCamperProfileHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Applica il PUT soltanto se il token If-Match coincide con il rowversion corrente.</summary>
    public async Task<UpdateCamperProfileResult> HandleAsync(
        Guid camperId,
        UpdateCamperProfileRequest request,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var camper = await FindOwnedCamperAsync(camperId, cancellationToken).ConfigureAwait(false);
        if (camper is null)
        {
            return new UpdateCamperProfileResult(UpdateCamperProfileOutcome.NotFound);
        }

        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return new UpdateCamperProfileResult(UpdateCamperProfileOutcome.MissingPrecondition);
        }

        if (!RowVersionEtag.TryParse(ifMatch, out var token)
            || camper.RowVersion is not { Length: > 0 } current
            || !current.AsSpan().SequenceEqual(token))
        {
            return new UpdateCamperProfileResult(UpdateCamperProfileOutcome.PreconditionFailed);
        }

        ApplyProfile(camper, request);
        camper.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        db.Entry(camper).Property(item => item.RowVersion).OriginalValue = token;

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new UpdateCamperProfileResult(UpdateCamperProfileOutcome.PreconditionFailed);
        }

        return new UpdateCamperProfileResult(
            UpdateCamperProfileOutcome.Updated,
            CamperProfileResponse.From(camper),
            camper.RowVersion);
    }

    private Task<Roamly.Domain.Entities.Camper?> FindOwnedCamperAsync(
        Guid camperId,
        CancellationToken cancellationToken)
    {
        return db.Campers
            .Where(item => item.OwnerId == db.CurrentUserId && item.Id == camperId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static void ApplyProfile(
        Roamly.Domain.Entities.Camper camper,
        UpdateCamperProfileRequest request)
    {
        camper.Name = request.Name!.Trim();
        camper.Brand = Normalize(request.Brand);
        camper.Model = Normalize(request.Model);
        camper.Year = request.Year;
        camper.PlateNumber = Normalize(request.PlateNumber);
        camper.VehicleKind = request.VehicleKind!.Value;
        camper.Dimensions = request.LengthMm is { } length
            ? new Dimensions(length, request.WidthMm!.Value, request.HeightMm!.Value)
            : null;
        camper.Weights = request.KerbWeightKg is { } kerbWeight
            ? new Weights(kerbWeight, request.MaxWeightKg!.Value)
            : null;
        camper.Capacities = request.FreshWaterL is { } freshWater
            ? new Capacities(freshWater, request.GreyWaterL!.Value, request.FuelTankL!.Value, request.GasKg!.Value)
            : null;
        camper.FuelKind = request.FuelKind;
        camper.AverageConsumptionLPer100Km = request.AverageConsumptionLPer100Km;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
