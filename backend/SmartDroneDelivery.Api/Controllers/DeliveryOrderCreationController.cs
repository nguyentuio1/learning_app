using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Route("api/delivery-orders")]
[Authorize(Roles = AppRoles.Customer)]
public class DeliveryOrderCreationController : ControllerBase
{
    private readonly IDeliveryOrderCreationService _creationService;

    public DeliveryOrderCreationController(IDeliveryOrderCreationService creationService)
    {
        _creationService = creationService;
    }

    [HttpPost]
    public async Task<ActionResult<CreatedDeliveryOrderDto>> Create([FromBody] CreateDeliveryOrderRequest request)
    {
        var created = await _creationService.CreateAsync(CurrentUserId(), request);
        return Created($"/api/delivery-orders/{created.Id}", created);
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");
}
