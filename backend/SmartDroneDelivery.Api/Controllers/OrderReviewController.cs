using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.Dispatcher + "," + AppRoles.Admin)]
public class OrderReviewController(IOrderReviewService reviews) : ControllerBase
{
    [HttpGet("api/dispatch/pending-orders")]
    public Task<PagedResult<PendingOrderDto>> Pending([FromQuery] PendingOrderFilterQuery query, CancellationToken ct) =>
        reviews.ListPendingAsync(CurrentUserId(), query, ct);

    [HttpPost("api/delivery-orders/{id:guid}/approve")]
    public Task<OrderDecisionDto> Approve(Guid id, CancellationToken ct) =>
        reviews.ApproveAsync(id, CurrentUserId(), HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);

    [HttpPost("api/delivery-orders/{id:guid}/reject")]
    public Task<OrderDecisionDto> Reject(Guid id, RejectOrderRequest request, CancellationToken ct) =>
        reviews.RejectAsync(id, CurrentUserId(), request.Reason, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");
}
