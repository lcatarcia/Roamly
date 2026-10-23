using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Campers.ListCampers;

/// <summary>Parametri query di <c>GET /api/v1/campers</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record ListCampersRequest(
    [property: FromQuery, Range(1, 200)] int? Limit,
    [property: FromQuery] string? After);
