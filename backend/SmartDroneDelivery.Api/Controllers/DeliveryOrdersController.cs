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
public class DeliveryOrdersController : ControllerBase
{
    private readonly IDeliveryOrderService _orderService;

    public DeliveryOrdersController(IDeliveryOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<ActionResult<DeliveryOrderDetailDto>> Create([FromBody] CreateDeliveryOrderRequest request)
    {
        var created = await _orderService.CreateAsync(CurrentUserId(), request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DeliveryOrderDetailDto>> Update(Guid id, [FromBody] UpdateDeliveryOrderRequest request)
    {
        return Ok(await _orderService.UpdateAsync(id, CurrentUserId(), CurrentUserRole(), request));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<DeliveryOrderDetailDto>> Cancel(Guid id, [FromBody] CancelDeliveryOrderRequest request)
    {
        return Ok(await _orderService.CancelAsync(id, CurrentUserId(), CurrentUserRole(), request));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<DeliveryOrderSummaryDto>>> List([FromQuery] DeliveryOrderFilterQuery query)
    {
        return Ok(await _orderService.ListAsync(CurrentUserId(), CurrentUserRole(), query));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeliveryOrderDetailDto>> GetById(Guid id)
    {
        return Ok(await _orderService.GetByIdAsync(id, CurrentUserId(), CurrentUserRole()));
    }

    [HttpGet("{id:guid}/status-history")]
    public async Task<ActionResult<IReadOnlyList<DeliveryStatusLogDto>>> GetStatusHistory(Guid id)
    {
        return Ok(await _orderService.GetStatusHistoryAsync(id, CurrentUserId(), CurrentUserRole()));
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");

    private string CurrentUserRole() =>
        User.FindFirst("role")?.Value ?? AppRoles.Customer;
}
