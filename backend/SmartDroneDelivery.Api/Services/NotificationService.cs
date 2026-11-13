using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<NotificationResponseDto>> ListAsync(Guid customerId, NotificationFilterQuery query)
    {
        await EnsureActiveCustomerAsync(customerId);
        ValidateFilterQuery(query);

        var baseQuery = _db.Notifications.AsNoTracking().Where(n => n.UserId == customerId);

        if (query.IsRead.HasValue)
        {
            baseQuery = baseQuery.Where(n => n.IsRead == query.IsRead.Value);
        }

        var totalCount = await baseQuery.CountAsync();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var items = await baseQuery
            .OrderByDescending(n => n.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(n => new NotificationResponseDto(
                n.Id,
                n.OrderId,
                n.Type,
                n.Title,
                n.Message,
                n.IsRead,
                DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc),
                n.ReadAt.HasValue ? DateTime.SpecifyKind(n.ReadAt.Value, DateTimeKind.Utc) : null))
            .ToListAsync();

        return new PagedResult<NotificationResponseDto>(items, query.Page, query.PageSize, totalCount, totalPages);
    }

    public async Task<UnreadCountResponseDto> GetUnreadCountAsync(Guid customerId)
    {
        await EnsureActiveCustomerAsync(customerId);

        var count = await _db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == customerId && !n.IsRead);

        return new UnreadCountResponseDto(count);
    }

    public async Task<NotificationResponseDto> MarkAsReadAsync(Guid customerId, Guid notificationId)
    {
        await EnsureActiveCustomerAsync(customerId);

        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId);

        if (notification == null || notification.UserId != customerId)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "Notification not found.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return new NotificationResponseDto(
            notification.Id,
            notification.OrderId,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.IsRead,
            DateTime.SpecifyKind(notification.CreatedAt, DateTimeKind.Utc),
            notification.ReadAt.HasValue ? DateTime.SpecifyKind(notification.ReadAt.Value, DateTimeKind.Utc) : null);
    }

    public async Task<ReadAllNotificationsResponseDto> MarkAllAsReadAsync(Guid customerId)
    {
        await EnsureActiveCustomerAsync(customerId);

        var unread = await _db.Notifications
            .Where(n => n.UserId == customerId && !n.IsRead)
            .ToListAsync();

        if (unread.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var item in unread)
            {
                item.IsRead = true;
                item.ReadAt = now;
            }
            await _db.SaveChangesAsync();
        }

        return new ReadAllNotificationsResponseDto(unread.Count);
    }

    private async Task EnsureActiveCustomerAsync(Guid customerId)
    {
        var user = await _db.Users.AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == customerId)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");

        if (user.Status != "ACTIVE")
        {
            throw new ApiException(StatusCodes.Status403Forbidden, "This account is not active.");
        }

        if (user.Role?.Code != AppRoles.Customer)
        {
            throw new ApiException(StatusCodes.Status403Forbidden, "Only customers can access customer notifications.");
        }
    }

    private static void ValidateFilterQuery(NotificationFilterQuery query)
    {
        var errors = new Dictionary<string, string[]>();

        if (query.Page < 1)
        {
            errors["page"] = ["Page must be at least 1."];
        }

        if (query.PageSize < 1 || query.PageSize > 100)
        {
            errors["pageSize"] = ["PageSize must be between 1 and 100."];
        }

        if (errors.Count > 0)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Invalid query parameters.", errors);
        }
    }
}
