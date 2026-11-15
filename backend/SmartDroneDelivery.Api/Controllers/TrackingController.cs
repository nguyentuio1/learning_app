using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/delivery-orders/{id}/tracking")]
public class TrackingController(ITrackingService tracking) : ControllerBase
{
    [HttpGet]
    public async Task<TrackingSnapshot> Get(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
            throw new ApiException(401, "Missing, invalid or expired token.");
        if (!Guid.TryParse(id, out var orderId))
            throw new ApiException(400, "A valid order UUID is required.");
        return (await tracking.GetAsync(userId, orderId, ct)).Snapshot;
    }
}
