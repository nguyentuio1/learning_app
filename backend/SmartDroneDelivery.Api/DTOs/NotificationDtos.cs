using System.ComponentModel.DataAnnotations;

namespace SmartDroneDelivery.Api.DTOs;

public record NotificationResponseDto(
    Guid Id,
    Guid? OrderId,
    string Type,
    string Title,
    string Message,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt);

public record UnreadCountResponseDto(int UnreadCount);

public record ReadAllNotificationsResponseDto(int UpdatedCount);

public class NotificationFilterQuery
{
    public bool? IsRead { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")]
    public int Page { get; set; } = 1;
    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = 20;
}
