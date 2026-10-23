using Roamly.Api.Common.Http;
using Roamly.Api.Features.Campers.Equipment;

namespace Roamly.Api.Features.Campers.Equipment.ListEquipment;

/// <summary>Pagina o esito di lookup/cursore della collection inventory.</summary>
public sealed record ListEquipmentResult(Page<EquipmentResponse>? Page, bool InvalidCursor, bool NotFound);
