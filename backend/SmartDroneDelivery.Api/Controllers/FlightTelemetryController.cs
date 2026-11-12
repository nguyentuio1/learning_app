using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

/// <summary>LA-331: optional ADMIN-only HTTP ingress for simulator tests and demos.</summary>
[ApiController]
[Route("api/flight-missions/{missionId:guid}/telemetry")]
[Authorize(Roles = AppRoles.Admin)]
public class FlightTelemetryController(IFlightTelemetryService telemetry) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ingest(Guid missionId, TelemetrySampleRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
            throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");
        await telemetry.IngestForAdminAsync(userId, missionId, request, ct);
        return NoContent();
    }
}
