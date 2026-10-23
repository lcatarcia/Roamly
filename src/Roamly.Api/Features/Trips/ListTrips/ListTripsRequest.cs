using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Trips.ListTrips;

/// <summary>Parametri query di <c>GET /api/v1/trips</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record ListTripsRequest(
    [property: FromQuery(Name = "limit"), RegularExpression(@"\A(?:[1-9]|[1-9][0-9]|1[0-9]{2}|200)\z")]
    string? Limit,
    [property: FromQuery(Name = "after")] string? After);
