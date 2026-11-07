using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

/// <summary>LA-118: Station slot management (docs/api/station-api.md sections 3.1 to 3.5).</summary>
[ApiController]
[Authorize]
public class StationSlotsController : ControllerBase
{
    private readonly IStationSlotService _slots;

    public StationSlotsController(IStationSlotService slots)
    {
        _slots = slots;
    }

    [HttpGet("api/landing-stations/{stationId:guid}/slots")]
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Dispatcher + "," + AppRoles.StationOperator)]
    public async Task<ActionResult<IReadOnlyList<StationSlotDto>>> ListByStation(Guid stationId)
    {
        return Ok(await _slots.ListByStationAsync(CurrentUserId(), stationId));
    }

    [HttpGet("api/station-slots/{id:guid}")]
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Dispatcher + "," + AppRoles.StationOperator)]
    public async Task<ActionResult<StationSlotDto>> GetById(Guid id)
    {
        return Ok(await _slots.GetByIdAsync(CurrentUserId(), id));
    }

    [HttpPost("api/landing-stations/{stationId:guid}/slots")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<StationSlotDto>> AddSlot(
        Guid stationId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AddStationSlotRequest? request = null)
    {
        var created = await _slots.AddSlotAsync(CurrentUserId(), stationId, request);
        return Created($"/api/station-slots/{created.Id}", created);
    }

    [HttpDelete("api/station-slots/{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> RemoveSlot(Guid id)
    {
        await _slots.RemoveSlotAsync(CurrentUserId(), id);
        return NoContent();
    }

    [HttpPut("api/station-slots/{id:guid}/status")]
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.StationOperator)]
    public async Task<ActionResult<StationSlotDto>> UpdateStatus(Guid id, UpdateStationSlotStatusRequest request)
    {
        return Ok(await _slots.UpdateStatusAsync(CurrentUserId(), id, request));
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");
}
