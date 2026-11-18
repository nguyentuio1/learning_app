using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public class DeliveryInsightLookupService(AppDbContext db)
{
    public async Task<PagedResult<DeliveryInsightOrderDto>> ListAsync(Guid caller, IQueryCollection parameters, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == caller)
            .Select(u => new { u.Status, Role = u.Role.Code }).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(401, "Missing, invalid or expired token.");
        if (user.Status != "ACTIVE" || user.Role is not (AppRoles.Dispatcher or AppRoles.Manager))
            throw new ApiException(403, "You do not have permission to look up delivery insights.");

        var filter = DeliveryInsightLookupQuery.Parse(parameters);
        var query = db.DeliveryOrders.AsNoTracking();
        if (filter.SearchKeyword is { } keyword)
        {
            var lower = keyword.ToLowerInvariant();
            query = query.Where(o => o.TrackingNumber.ToLower().Contains(lower));
        }
        if (filter.Status is { } status) query = query.Where(o => o.Status == status);
        if (filter.FromUtc is { } from) query = query.Where(o => o.CreatedAt >= from);
        if (filter.ToExclusiveUtc is { } to) query = query.Where(o => o.CreatedAt < to);

        var count = await query.CountAsync(ct);
        var offset = (long)(filter.Page - 1) * filter.PageSize;
        IReadOnlyList<DeliveryInsightOrderDto> items = offset > int.MaxValue ? [] : await query
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip((int)offset).Take(filter.PageSize)
            .Select(o => new DeliveryInsightOrderDto(o.Id, o.TrackingNumber, o.Status,
                o.OriginStation.Name, o.DestinationStation.Name, DateTime.SpecifyKind(o.CreatedAt, DateTimeKind.Utc)))
            .ToListAsync(ct);
        return new(items, filter.Page, filter.PageSize, count, (int)Math.Ceiling((double)count / filter.PageSize));
    }
}
