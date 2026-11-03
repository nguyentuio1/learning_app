using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

/// <summary>Customer delivery orders. See docs/api/customer-order-api.md section 5.</summary>
[ApiController]
[Route("api/delivery-orders")]
[Authorize(Roles = AppRoles.Customer)]
public class DeliveryOrdersController : ControllerBase
{
    private readonly IDeliveryOrderService _orders;

    public DeliveryOrdersController(IDeliveryOrderService orders)
    {
        _orders = orders;
    }

    /// <summary>LA-103: create an order. The only response that contains the pickup code.</summary>
    [HttpPost]
    public async Task<ActionResult<CreatedDeliveryOrderDto>> Create(CreateDeliveryOrderRequest request)
    {
        var created = await _orders.CreateAsync(CurrentUserId(), request);
        // GET /api/delivery-orders/{id} is LA-111, so the location is a plain URL for now.
        return Created($"/api/delivery-orders/{created.Id}", created);
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");
}
