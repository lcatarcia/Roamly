using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Campers.Equipment.ListEquipment;

/// <summary>Parametri keyset di <c>GET /api/v1/campers/{camperId}/equipment</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record ListEquipmentRequest(
    [property: FromQuery, Range(1, 200)] int? Limit,
    [property: FromQuery] string? After);
