using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Campers.ListMaintenance;

/// <summary>Parametri di <c>GET /api/v1/campers/{camperId}/maintenance</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record ListMaintenanceRequest(
    [property: FromQuery, Range(1, 200)] int? Limit,
    [property: FromQuery] string? After);
