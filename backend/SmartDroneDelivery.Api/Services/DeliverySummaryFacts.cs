using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

// Provider-facing records deliberately exclude database IDs, actor IDs and all free text.
public record SummaryEvent(string? FromStatus, string ToStatus, DateTime OccurredAt);
public record SummaryModelInput(string CurrentStatus, SummaryEvent[] Events);
public record DeliverySummarySnapshot(Guid OwnerId, DateTime OrderUpdatedAt, SummaryModelInput Facts, long[] LogIds)
{
    public string Stamp => JsonSerializer.Serialize(new { OwnerId, OrderUpdatedAt, Facts, LogIds });
}

public class DeliverySummaryFacts(AppDbContext db)
{
    public const int MaximumEvents = 200;

    public async Task<DeliverySummarySnapshot> ReadAsync(Guid userId, Guid orderId, CancellationToken ct)
    {
        // End the repeatable-read transaction before any inference, including its final recheck.
        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct) : null;
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.Status, Role = u.Role.Code }).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(401, "Missing, invalid or expired token.");
        if (user.Status != "ACTIVE" || user.Role is not (AppRoles.Customer or AppRoles.Dispatcher or AppRoles.Manager))
            throw new ApiException(403, "You do not have permission to summarize deliveries.");
        var order = await db.DeliveryOrders.AsNoTracking().Where(o => o.Id == orderId
                && (user.Role != AppRoles.Customer || o.CustomerId == userId))
            .Select(o => new { o.CustomerId, o.Status, o.UpdatedAt }).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(404, "Delivery order not found.");
        var logs = await db.DeliveryStatusLogs.AsNoTracking().Where(l => l.OrderId == orderId)
            .OrderBy(l => l.CreatedAt).ThenBy(l => l.Id).Take(MaximumEvents + 1)
            .Select(l => new { l.Id, l.FromStatus, l.ToStatus, l.CreatedAt }).ToArrayAsync(ct);
        if (logs.Length > MaximumEvents) throw new ApiException(503, "The recorded history exceeds the summary limit.");
        if (tx is not null) await tx.CommitAsync(ct);
        return new(order.CustomerId, order.UpdatedAt,
            new(order.Status, logs.Select(l => new SummaryEvent(l.FromStatus, l.ToStatus,
                DateTime.SpecifyKind(l.CreatedAt, DateTimeKind.Utc))).ToArray()), logs.Select(l => l.Id).ToArray());
    }
}
