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
public class DeliveryOrderQueryController : ControllerBase
{
    private readonly IDeliveryOrderQueryService _queryService;

    public DeliveryOrderQueryController(IDeliveryOrderQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<DeliveryOrderSummaryDto>>> List([FromQuery] DeliveryOrderFilterQuery query)
    {
        return Ok(await _queryService.ListAsync(CurrentUserId(), query));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeliveryOrderDetailDto>> GetById(Guid id)
    {
        return Ok(await _queryService.GetByIdAsync(id, CurrentUserId()));
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");
}
