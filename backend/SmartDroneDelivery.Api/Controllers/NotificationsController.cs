using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Roles = AppRoles.Customer)]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationResponseDto>>> List([FromQuery] NotificationFilterQuery query)
    {
        return Ok(await _notificationService.ListAsync(CurrentUserId(), query));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponseDto>> GetUnreadCount()
    {
        return Ok(await _notificationService.GetUnreadCountAsync(CurrentUserId()));
    }

    [HttpPut("{id:guid}/read")]
    public async Task<ActionResult<NotificationResponseDto>> MarkAsRead(Guid id)
    {
        return Ok(await _notificationService.MarkAsReadAsync(CurrentUserId(), id));
    }

    [HttpPut("read-all")]
    public async Task<ActionResult<ReadAllNotificationsResponseDto>> MarkAllAsRead()
    {
        return Ok(await _notificationService.MarkAllAsReadAsync(CurrentUserId()));
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");
}
