using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Authorize]
public class FailureController(FailureService failures) : ControllerBase
{
    private const string StaffRoles = AppRoles.Dispatcher + "," + AppRoles.Admin;
    [HttpPost("api/delivery-orders/{id}/fail")]
    [Authorize(Roles = StaffRoles)]
    public Task<OrderDecisionDto> Report(Guid id, ReportFailureRequest request, CancellationToken ct) =>
        failures.ReportAsync(id, ActorId(), request, ct);
    [HttpPost("api/delivery-orders/{id}/resolve-failure")]
    [Authorize(Roles = StaffRoles)]
    public Task<OrderDecisionDto> Resolve(Guid id, ResolveFailureRequest request, CancellationToken ct) =>
        failures.ResolveAsync(id, ActorId(), request, ct);
    [HttpGet("api/dispatch/failed-orders")]
    [Authorize(Roles = StaffRoles)]
    public Task<PagedResult<FailedOrderDto>> List([FromQuery] FailedOrderFilterQuery query, CancellationToken ct) => failures.ListAsync(ActorId(), query, ct);
    [HttpGet("api/dispatch/failed-orders/{id}")]
    [Authorize(Roles = StaffRoles)]
    public Task<FailureOrderDetailDto> Detail(Guid id, CancellationToken ct) => failures.DetailAsync(id, ActorId(), ct);
    [HttpGet("api/dispatch/active-deliveries")]
    [Authorize(Roles = StaffRoles + "," + AppRoles.Manager)]
    public Task<IReadOnlyList<ActiveDeliveryDto>> Active([FromQuery] Guid? stationId, CancellationToken ct) => failures.ActiveAsync(ActorId(), stationId, ct);
    private Guid ActorId() => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id : throw new ApiException(401, "Missing, invalid or expired token.");
}
