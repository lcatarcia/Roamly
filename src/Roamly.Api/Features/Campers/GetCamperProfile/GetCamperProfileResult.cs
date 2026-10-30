namespace Roamly.Api.Features.Campers.GetCamperProfile;

/// <summary>Risorsa del profilo e token usato dall'endpoint per costruire l'ETag HTTP.</summary>
public sealed record GetCamperProfileResult(CamperProfileResponse Profile, byte[] RowVersion);
