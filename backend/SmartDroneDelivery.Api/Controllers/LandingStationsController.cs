using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

/// <summary>LA-115: Landing station management (docs/api/station-api.md sections 2.1 to 2.5).</summary>
[ApiController]
[Route("api/landing-stations")]
[Authorize]
public class LandingStationsController : ControllerBase
{
    private readonly ILandingStationService _stations;

    public LandingStationsController(ILandingStationService stations)
    {
        _stations = stations;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LandingStationDto>>> List(
        [FromQuery] string? status, [FromQuery] string? search)
    {
        return Ok(await _stations.ListAsync(CurrentUserId(), status, search));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Dispatcher + "," + AppRoles.StationOperator)]
    public async Task<ActionResult<LandingStationDetailDto>> GetById(Guid id)
    {
        return Ok(await _stations.GetByIdAsync(CurrentUserId(), id));
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<LandingStationDto>> Create(CreateLandingStationRequest request)
    {
        var created = await _stations.CreateAsync(CurrentUserId(), request);
        return Created($"/api/landing-stations/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<LandingStationDto>> Update(Guid id, UpdateLandingStationRequest request)
    {
        return Ok(await _stations.UpdateAsync(CurrentUserId(), id, request));
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.StationOperator)]
    public async Task<ActionResult<LandingStationStatusChangeResponseDto>> ChangeStatus(
        Guid id, ChangeLandingStationStatusRequest request)
    {
        return Ok(await _stations.ChangeStatusAsync(CurrentUserId(), id, request));
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");
}
