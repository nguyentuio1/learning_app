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
public class DeliveryOrderMutationController : ControllerBase
{
    private readonly IDeliveryOrderMutationService _mutationService;

    public DeliveryOrderMutationController(IDeliveryOrderMutationService mutationService)
    {
        _mutationService = mutationService;
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DeliveryOrderDetailDto>> Update(Guid id, [FromBody] UpdateDeliveryOrderRequest request)
    {
        return Ok(await _mutationService.UpdateAsync(id, CurrentUserId(), request));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<DeliveryOrderDetailDto>> Cancel(Guid id, [FromBody] CancelDeliveryOrderRequest request)
    {
        return Ok(await _mutationService.CancelAsync(id, CurrentUserId(), request));
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");
}
