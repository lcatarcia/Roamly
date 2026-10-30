using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Campers.ListMaintenanceLogs;

/// <summary>Parametri di <c>GET .../maintenance/{maintenanceItemId}/logs</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record ListMaintenanceLogsRequest(
    [property: FromQuery, Range(1, 200)] int? Limit,
    [property: FromQuery] string? After);
