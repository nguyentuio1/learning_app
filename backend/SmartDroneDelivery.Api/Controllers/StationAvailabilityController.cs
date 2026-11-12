using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

/// <summary>LA-121: Station availability monitoring (docs/api/station-api.md section 2.6).</summary>
[ApiController]
[Route("api/landing-stations")]
[Authorize]
public class StationAvailabilityController : ControllerBase
{
    private readonly IStationAvailabilityService _service;

    public StationAvailabilityController(IStationAvailabilityService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}/availability")]
    public async Task<ActionResult<StationAvailabilityDto>> GetAvailability(Guid id)
    {
        return Ok(await _service.GetAvailabilityAsync(CurrentUserId(), id));
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");
}
