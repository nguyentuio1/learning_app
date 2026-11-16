using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.Customer)]
[Route("api/delivery-orders/{id}/confirm-delivery")]
public class DeliveryConfirmationController(IStationOperationService operations) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Confirm(string id, ConfirmDeliveryRequest request)
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
            throw new ApiException(401, "Missing, invalid or expired token.");
        if (!Guid.TryParse(id, out var orderId)) throw new ApiException(400, "A valid order UUID is required.");
        return PickupResponses.Create(this, await operations.ConfirmDeliveryAsync(userId, orderId, request));
    }
}
