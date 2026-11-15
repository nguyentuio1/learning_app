using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public interface IOrderReviewService
{
    Task<PagedResult<PendingOrderDto>> ListPendingAsync(Guid actorId, PendingOrderFilterQuery query, CancellationToken ct = default);
    Task<OrderDecisionDto> ApproveAsync(Guid orderId, Guid actorId, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);
    Task<OrderDecisionDto> RejectAsync(Guid orderId, Guid actorId, string? reason, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);
}

public class OrderReviewService(AppDbContext db, IOrderTransitionWriter transitions) : IOrderReviewService
{
    public async Task<PagedResult<PendingOrderDto>> ListPendingAsync(Guid actorId, PendingOrderFilterQuery query, CancellationToken ct = default)
    {
        await EnsureDispatcherAsync(actorId, ct);
        if (query.Page < 1) throw FieldError("page", "Page must be at least 1.");
        if (query.PageSize is < 1 or > 100) throw FieldError("pageSize", "Page size must be between 1 and 100.");
        if (query.FromDate > query.ToDate) throw FieldError("toDate", "To date must be on or after from date.");

        var orders = db.DeliveryOrders.AsNoTracking().Where(o => o.Status == "PENDING");
        if (query.OriginStationId.HasValue) orders = orders.Where(o => o.OriginStationId == query.OriginStationId.Value);
        if (query.DestinationStationId.HasValue) orders = orders.Where(o => o.DestinationStationId == query.DestinationStationId.Value);
        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value.UtcDateTime;
            orders = orders.Where(o => o.CreatedAt >= from);
        }
        if (query.ToDate.HasValue)
        {
            var to = query.ToDate.Value.UtcDateTime;
            orders = orders.Where(o => o.CreatedAt <= to);
        }
        var total = await orders.CountAsync(ct);
        var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset >= total ? [] : await orders.OrderBy(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip((int)offset).Take(query.PageSize)
            .Select(o => new PendingOrderDto(o.Id, o.TrackingNumber, o.Customer.FullName, o.Customer.PhoneNumber,
                o.Package.PackageName, o.Package.WeightGram, o.Package.LengthCm, o.Package.WidthCm, o.Package.HeightCm, o.Package.IsFragile,
                new ReviewStationDto(o.OriginStation.Id, o.OriginStation.StationCode, o.OriginStation.Name),
                new ReviewStationDto(o.DestinationStation.Id, o.DestinationStation.StationCode, o.DestinationStation.Name), o.CreatedAt))
            .ToListAsync(ct);
        return new(items.Select(o => o with { CreatedAt = DateTime.SpecifyKind(o.CreatedAt, DateTimeKind.Utc) }).ToList(),
            query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public Task<OrderDecisionDto> ApproveAsync(Guid orderId, Guid actorId, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default) =>
        DecideAsync(orderId, actorId, true, null, ipAddress, userAgent, ct);

    public Task<OrderDecisionDto> RejectAsync(Guid orderId, Guid actorId, string? reason, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default) =>
        DecideAsync(orderId, actorId, false, reason, ipAddress, userAgent, ct);

    private async Task<OrderDecisionDto> DecideAsync(Guid orderId, Guid actorId, bool approve, string? reason,
        string? ipAddress, string? userAgent, CancellationToken ct)
    {
        using var operation = await transitions.EnterOperationAsync(ct);
        await EnsureDispatcherAsync(actorId, ct);
        reason = reason?.Trim();
        if (!approve && (string.IsNullOrEmpty(reason) || reason.Length > 500))
            throw FieldError("reason", "Reason must contain 1 to 500 characters.");

        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            // Lock the order before reading its station ids: customer edits must not change
            // the route between station validation and the status compare-and-set.
            var order = db.Database.IsNpgsql()
                ? (await db.DeliveryOrders.FromSqlInterpolated($"SELECT * FROM delivery_orders WHERE \"Id\" = {orderId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
                : await db.DeliveryOrders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order is null) throw new ApiException(StatusCodes.Status404NotFound, "Delivery order not found.");
            if (order.Status != "PENDING") throw Conflict();

            if (approve)
            {
                // SHARE blocks concurrent station-status updates. Consistent UUID order also
                // handles opposite routes without taking station locks in reverse order.
                var stations = db.Database.IsNpgsql()
                    ? await db.LandingStations.FromSqlInterpolated($"SELECT * FROM landing_stations WHERE \"Id\" IN ({order.OriginStationId}, {order.DestinationStationId}) ORDER BY \"Id\" FOR SHARE").AsNoTracking().ToListAsync(ct)
                    : await db.LandingStations.AsNoTracking().Where(s => s.Id == order.OriginStationId || s.Id == order.DestinationStationId).ToListAsync(ct);
                foreach (var stationId in new[] { order.OriginStationId, order.DestinationStationId })
                {
                    var station = stations.SingleOrDefault(s => s.Id == stationId);
                    if (station is null || station.Status != "OPERATIONAL")
                        throw new ApiException(StatusCodes.Status409Conflict,
                            $"Station {station?.Name ?? stationId.ToString()} is not OPERATIONAL.");
                }
            }

            var now = DateTime.UtcNow;
            var status = approve ? "APPROVED" : "CANCELLED";
            await transitions.StageAsync(order, "PENDING", status, actorId, approve ? "Order approved" : reason, now, ct);
            if (approve) order.DispatcherId = actorId;
            else order.CancelReason = reason;
            db.Notifications.Add(new Notification
            {
                UserId = order.CustomerId, OrderId = order.Id, Type = "ORDER_STATUS_CHANGED",
                Title = approve ? "Order approved" : "Order rejected",
                Message = approve ? $"Your delivery order {order.TrackingNumber} has been approved."
                    : $"Your delivery order {order.TrackingNumber} has been rejected: {reason}",
                CreatedAt = now
            });
            db.AuditLogs.Add(new AuditLog
            {
                UserId = actorId, Action = approve ? "ORDER_APPROVAL" : "ORDER_REJECTION",
                EntityName = "DeliveryOrder", EntityId = order.Id.ToString(), IpAddress = ipAddress,
                UserAgent = userAgent, Timestamp = now
            });
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return new(order.Id, order.TrackingNumber, status, now);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw Conflict();
        }
    }

    private async Task EnsureDispatcherAsync(Guid actorId, CancellationToken ct)
    {
        var allowed = await db.Users.AsNoTracking().AnyAsync(u => u.Id == actorId && u.Status == "ACTIVE"
            && (u.Role.Code == AppRoles.Dispatcher || u.Role.Code == AppRoles.Admin), ct);
        if (!allowed) throw new ApiException(StatusCodes.Status403Forbidden, "An active dispatcher or admin account is required.");
    }

    private static ApiException Conflict() => new(StatusCodes.Status409Conflict, "Only a current PENDING order can be reviewed; the order may have changed.");
    private static ApiException FieldError(string field, string message) => new(StatusCodes.Status400BadRequest,
        "One or more fields are invalid.", new Dictionary<string, string[]> { [field] = [message] });
}
