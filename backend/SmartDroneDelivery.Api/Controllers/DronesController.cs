using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

/// <summary>LA-128: Drone management API (docs/api/drone-api.md sections 2 and 3).</summary>
[ApiController]
[Route("api/drones")]
[Authorize(Roles = AppRoles.Dispatcher + "," + AppRoles.Admin)]
public class DronesController : ControllerBase
{
    private readonly IDroneService _drones;

    public DronesController(IDroneService drones)
    {
        _drones = drones;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DroneDto>>> List(
        [FromQuery] string? status, [FromQuery] Guid? stationId, [FromQuery] string? search)
    {
        return Ok(await _drones.ListAsync(CurrentUserId(), status, stationId, search));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DroneDetailDto>> GetById(Guid id)
    {
        return Ok(await _drones.GetByIdAsync(CurrentUserId(), id));
    }

    [HttpPost]
    public async Task<ActionResult<DroneDto>> Create(CreateDroneRequest request)
    {
        var created = await _drones.CreateAsync(CurrentUserId(), request);
        return Created($"/api/drones/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DroneDto>> Update(Guid id, UpdateDroneRequest request)
    {
        return Ok(await _drones.UpdateAsync(CurrentUserId(), id, request));
    }

    [HttpPut("{id:guid}/maintenance")]
    public async Task<ActionResult<DroneDto>> Maintenance(Guid id, DroneMaintenanceRequest request)
    {
        return Ok(await _drones.MaintenanceAsync(CurrentUserId(), id, request));
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");
}
