using SmartDroneDelivery.Api.DTOs;

namespace SmartDroneDelivery.Api.Services;

public interface INotificationService
{
    Task<PagedResult<NotificationResponseDto>> ListAsync(Guid customerId, NotificationFilterQuery query);
    Task<UnreadCountResponseDto> GetUnreadCountAsync(Guid customerId);
    Task<NotificationResponseDto> MarkAsReadAsync(Guid customerId, Guid notificationId);
    Task<ReadAllNotificationsResponseDto> MarkAllAsReadAsync(Guid customerId);
}
