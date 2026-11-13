using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.Dispatcher + "," + AppRoles.Admin)]
public class SchedulingController(ISchedulingService scheduling) : ControllerBase
{
    [HttpPut("api/delivery-orders/{id}/schedule")]
    public Task<ScheduleDto> Schedule(Guid id, ScheduleOrderRequest request, CancellationToken ct) =>
        scheduling.ScheduleAsync(id, ActorId(), request, ct);

    [HttpGet("api/delivery-orders/{id}/available-drones")]
    public Task<IReadOnlyList<DroneCandidateDto>> Candidates(Guid id, CancellationToken ct) => scheduling.CandidatesAsync(id, ActorId(), ct);

    [HttpPost("api/delivery-orders/{id}/flight-mission")]
    public async Task<ActionResult<FlightMissionDto>> Assign(Guid id, AssignDroneRequest request, CancellationToken ct)
    {
        var result = await scheduling.AssignAsync(id, ActorId(), request, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
        return Created($"/api/delivery-orders/{id}/flight-mission", result);
    }

    [HttpGet("api/dispatch/approved-orders")]
    public Task<PagedResult<ApprovedOrderDto>> Queue([FromQuery] ApprovedOrderFilterQuery query, CancellationToken ct) =>
        scheduling.ListApprovedAsync(ActorId(), query, ct);

    [HttpGet("api/dispatch/orders/{id}")]
    public Task<DispatchOrderDetailDto> Detail(Guid id, CancellationToken ct) => scheduling.DetailAsync(id, ActorId(), ct);

    private Guid ActorId() => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");
}
