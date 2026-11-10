using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public interface IPickupTimeoutService
{
    /// <summary>Fails every order that has been <c>ARRIVED</c> for longer than the pickup window. Returns how many were failed.</summary>
    Task<int> ExpireOverdueAsync(DateTime now, CancellationToken cancellationToken = default);
}

/// <summary>
/// LA-125, contract 4.6: an order that stays in <c>ARRIVED</c> for 24 hours moves to <c>FAILED</c> with
/// <c>cancelReason = "Pickup window expired (24h timeout)"</c>, and the dispatcher and the station operator are told.
/// The arrival time is the time of the status log that moved the order to <c>ARRIVED</c>.
/// </summary>
public class PickupTimeoutService : IPickupTimeoutService
{
    public const string CancelReason = "Pickup window expired (24h timeout)";
    public const string LogRemarks = "Automated 24h pickup timeout expiration";
    public const string AuditAction = "ORDER_PICKUP_TIMEOUT";
    private const int BatchSize = 100;

    private readonly AppDbContext _db;
    private readonly PickupTimeoutOptions _options;

    public PickupTimeoutService(AppDbContext db, IOptions<PickupTimeoutOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<int> ExpireOverdueAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var cutoff = now.AddHours(-_options.WindowHours);

        var candidates = await _db.DeliveryOrders
            .AsNoTracking()
            .Where(o => o.Status == "ARRIVED")
            .Select(o => new
            {
                o.Id,
                ArrivedAt = o.StatusLogs.Where(l => l.ToStatus == "ARRIVED").Max(l => (DateTime?)l.CreatedAt)
            })
            .Where(x => x.ArrivedAt != null && x.ArrivedAt <= cutoff)
            .OrderBy(x => x.ArrivedAt)
            .Take(BatchSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var orderId in candidates)
        {
            if (await ExpireOneAsync(orderId, now, cutoff, cancellationToken))
                expired++;
            _db.ChangeTracker.Clear();
        }

        return expired;
    }

    private async Task<bool> ExpireOneAsync(Guid orderId, DateTime now, DateTime cutoff, CancellationToken ct)
    {
        await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;

        // Same row lock as the pickup: a pickup that is running now either finishes first (the order is no longer
        // ARRIVED and is skipped here) or waits for this job, then sees FAILED and answers 409.
        if (tx != null)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM delivery_orders WHERE \"Id\" = {orderId} FOR UPDATE", ct);
        }

        var order = await _db.DeliveryOrders
            .Include(o => o.DestinationStation)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null || order.Status != "ARRIVED")
            return false;

        var arrivedAt = await _db.DeliveryStatusLogs
            .Where(l => l.OrderId == orderId && l.ToStatus == "ARRIVED")
            .MaxAsync(l => (DateTime?)l.CreatedAt, ct);
        if (arrivedAt is null || arrivedAt > cutoff)
            return false;

        order.Status = "FAILED";
        order.CancelReason = CancelReason;
        order.UpdatedAt = now;

        _db.DeliveryStatusLogs.Add(new DeliveryStatusLog
        {
            OrderId = order.Id,
            FromStatus = "ARRIVED",
            ToStatus = "FAILED",
            ChangedBy = null,
            Remarks = LogRemarks,
            CreatedAt = now
        });

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = null,
            Action = AuditAction,
            EntityName = "DeliveryOrder",
            EntityId = order.Id.ToString(),
            Timestamp = now
        });

        _db.Notifications.Add(new Notification
        {
            UserId = order.CustomerId,
            OrderId = order.Id,
            Type = "ORDER_STATUS",
            Title = "Pickup Expired",
            Message = "Your package was not retrieved within 24 hours. Please contact customer support.",
            CreatedAt = now
        });

        foreach (var recipientId in await StaffRecipientsAsync(order, ct))
        {
            _db.Notifications.Add(new Notification
            {
                UserId = recipientId,
                OrderId = order.Id,
                Type = "ORDER_STATUS",
                Title = "Pickup window expired",
                Message = $"Order {order.TrackingNumber} was not collected within {_options.WindowHours} hours and has failed. Move the parcel out of its slot.",
                CreatedAt = now
            });
        }

        await _db.SaveChangesAsync(ct);
        if (tx != null)
            await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>Staff to alert: the dispatcher of the order (every active dispatcher when it has none) and the operator of the destination station.</summary>
    private async Task<List<Guid>> StaffRecipientsAsync(DeliveryOrder order, CancellationToken ct)
    {
        var recipients = new HashSet<Guid>();

        if (order.DispatcherId is { } dispatcherId)
        {
            recipients.Add(dispatcherId);
        }
        else
        {
            var dispatchers = await _db.Users
                .AsNoTracking()
                .Where(u => u.Status == "ACTIVE" && u.Role.Code == AppRoles.Dispatcher)
                .Select(u => u.Id)
                .ToListAsync(ct);
            recipients.UnionWith(dispatchers);
        }

        if (order.DestinationStation.OperatorId is { } operatorId)
            recipients.Add(operatorId);

        return recipients.ToList();
    }
}

/// <summary>Runs <see cref="IPickupTimeoutService"/> on a timer for the life of the application.</summary>
public class PickupTimeoutWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly PickupTimeoutOptions _options;
    private readonly ILogger<PickupTimeoutWorker> _logger;

    public PickupTimeoutWorker(IServiceScopeFactory scopes, IOptions<PickupTimeoutOptions> options, ILogger<PickupTimeoutWorker> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes)));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IPickupTimeoutService>();
                    var expired = await service.ExpireOverdueAsync(DateTime.UtcNow, stoppingToken);
                    if (expired > 0)
                        _logger.LogInformation("Pickup timeout failed {Count} order(s).", expired);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Pickup timeout run failed; it will be tried again at the next interval.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Application is stopping.
        }
    }
}
