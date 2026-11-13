using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

public interface IOrderTransitionWriter
{
    /// <summary>Serializes nonrelational operations before loading the order; relational operations use database transactions.</summary>
    Task<IDisposable> EnterOperationAsync(CancellationToken ct = default);
    /// <summary>Persists status, log and customer notification; joins the caller's transaction when present.</summary>
    Task ApplyAsync(Guid orderId, string expectedFrom, string to, Guid? actorId, string? remarks, CancellationToken ct = default);
    /// <summary>
    /// Stages status and log for an existing operation that supplies its own notification and effects.
    /// Relational callers must own a transaction, SaveChanges and commit together, or roll back on failure.
    /// </summary>
    Task<DeliveryStatusLog> StageAsync(DeliveryOrder order, string expectedFrom, string to, Guid? actorId, string? remarks, DateTime now, CancellationToken ct = default);
}

public static class OrderStateMachine
{
    public static bool CanTransition(string from, string to) => (from, to) switch
    {
        ("PENDING", "APPROVED" or "CANCELLED") => true,
        // Intake rejection is part of the merged station contract (section 4.3).
        ("APPROVED", "READY_FOR_TAKEOFF" or "CANCELLED" or "FAILED") => true,
        ("READY_FOR_TAKEOFF", "IN_TRANSIT" or "CANCELLED") => true,
        ("IN_TRANSIT", "ARRIVED" or "FAILED") => true,
        ("ARRIVED", "DELIVERED" or "FAILED") => true,
        ("FAILED", "APPROVED" or "CANCELLED") => true,
        _ => false
    };
}

public class OrderTransitionWriter(AppDbContext db) : IOrderTransitionWriter
{
    // InMemory SaveChanges cannot roll back inserts when an order concurrency check fails.
    // All operation services use this shared gate, rather than separate per-service locks.
    private static readonly SemaphoreSlim NonRelationalOperations = new(1, 1);

    public async Task<IDisposable> EnterOperationAsync(CancellationToken ct = default)
    {
        if (db.Database.IsRelational()) return new OperationScope(null);
        await NonRelationalOperations.WaitAsync(ct);
        return new OperationScope(NonRelationalOperations);
    }

    private sealed class OperationScope(SemaphoreSlim? gate) : IDisposable
    {
        private SemaphoreSlim? _gate = gate;
        public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
    }

    public async Task ApplyAsync(Guid orderId, string expectedFrom, string to, Guid? actorId, string? remarks, CancellationToken ct = default)
    {
        using var operation = await EnterOperationAsync(ct);
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var order = await db.DeliveryOrders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "Delivery order not found.");
            var now = DateTime.UtcNow;
            await StageAsync(order, expectedFrom, to, actorId, remarks, now, ct);
            db.Notifications.Add(new Notification
            {
                UserId = order.CustomerId,
                OrderId = order.Id,
                Type = "ORDER_STATUS_CHANGED",
                Title = "Order status changed",
                Message = $"Your delivery order {order.TrackingNumber} is now {to}.",
                CreatedAt = now
            });
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw Conflict();
        }
    }

    public async Task<DeliveryStatusLog> StageAsync(DeliveryOrder order, string expectedFrom, string to, Guid? actorId, string? remarks, DateTime now, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!OrderStateMachine.CanTransition(expectedFrom, to) || order.Status != expectedFrom)
            throw Conflict();
        var entry = db.Entry(order);
        if (entry.State is EntityState.Detached or EntityState.Added or EntityState.Deleted)
            throw new InvalidOperationException("A transition requires a tracked, persisted order.");
        if (entry.Property(o => o.Status).OriginalValue != expectedFrom)
            throw Conflict();

        if (db.Database.IsRelational())
        {
            if (db.Database.CurrentTransaction is null)
                throw new InvalidOperationException("StageAsync requires a transaction for status, log and operation effects.");

            var changed = await db.DeliveryOrders.Where(o => o.Id == order.Id && o.Status == expectedFrom)
                .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.Status, to).SetProperty(o => o.UpdatedAt, now), ct);
            if (changed == 0) throw Conflict();

            // ExecuteUpdate bypasses tracking. Synchronize these two columns without touching
            // the caller's pending parcel, mission, cancellation or audit changes.
            entry.Property(o => o.Status).OriginalValue = to;
            entry.Property(o => o.UpdatedAt).OriginalValue = now;
        }
        order.Status = to;
        order.UpdatedAt = now;
        var log = new DeliveryStatusLog
        {
            OrderId = order.Id,
            FromStatus = expectedFrom,
            ToStatus = to,
            ChangedBy = actorId,
            Remarks = remarks,
            CreatedAt = now
        };
        db.DeliveryStatusLogs.Add(log);
        return log;
    }

    private static ApiException Conflict() => new(StatusCodes.Status409Conflict,
        "The order status changed or the requested transition is not allowed.");
}
