using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>
/// LA-125, docs/api/station-api.md section 4.6: an order that stays in ARRIVED for 24 hours fails with
/// cancelReason "Pickup window expired (24h timeout)" and the dispatcher and the station operator are alerted.
/// </summary>
public class PickupTimeoutTests : IClassFixture<AuthApiFactory>
{
    private static int _counter;
    private readonly AuthApiFactory _factory;

    public PickupTimeoutTests(AuthApiFactory factory) => _factory = factory;

    private record Seeded(Guid OrderId, Guid CustomerId, Guid OperatorId, Guid DispatcherId);

    private static User NewUser(Role role, int n, string tag) => new()
    {
        Id = Guid.NewGuid(),
        RoleId = role.Id,
        Email = $"{tag}-{n}@timeout.test",
        PasswordHash = "x",
        FullName = $"{tag} {n}",
        PhoneNumber = $"086{n:D7}",
        Status = "ACTIVE"
    };

    /// <summary>Seeds an ARRIVED order whose "ARRIVED" status log is <paramref name="arrivedHoursAgo"/> hours old.</summary>
    private async Task<Seeded> SeedArrivedOrderAsync(double arrivedHoursAgo, string status = "ARRIVED", bool withDispatcher = true)
    {
        var n = Interlocked.Increment(ref _counter);
        Seeded? seeded = null;
        await _factory.WithDbAsync(async db =>
        {
            var customer = NewUser(await db.Roles.SingleAsync(r => r.Code == AppRoles.Customer), n, "cust");
            var op = NewUser(await db.Roles.SingleAsync(r => r.Code == AppRoles.StationOperator), n, "op");
            var dispatcher = NewUser(await db.Roles.SingleAsync(r => r.Code == AppRoles.Dispatcher), n, "disp");
            db.Users.AddRange(customer, op, dispatcher);

            var origin = new LandingStation { Id = Guid.NewGuid(), StationCode = $"TO-O{n:D4}", Name = $"Origin {n}", Address = "1 St", Latitude = 10.7m, Longitude = 106.7m, TotalSlots = 1, Status = "OPERATIONAL" };
            var dest = new LandingStation { Id = Guid.NewGuid(), StationCode = $"TO-D{n:D4}", Name = $"Dest {n}", Address = "2 St", Latitude = 10.8m, Longitude = 106.8m, TotalSlots = 1, Status = "OPERATIONAL", OperatorId = op.Id };
            var package = new Package { SenderId = customer.Id, PackageName = "Box", WeightGram = 300, LengthCm = 10, WidthCm = 10, HeightCm = 5 };
            var order = new DeliveryOrder
            {
                TrackingNumber = $"DD-TO-{n:D4}",
                CustomerId = customer.Id,
                Package = package,
                OriginStationId = origin.Id,
                DestinationStationId = dest.Id,
                DispatcherId = withDispatcher ? dispatcher.Id : null,
                RecipientName = "R",
                RecipientPhone = "0909000000",
                PickupSecureCode = "123456",
                Status = status
            };
            db.LandingStations.AddRange(origin, dest);
            db.DeliveryOrders.Add(order);
            db.DeliveryStatusLogs.Add(new DeliveryStatusLog
            {
                OrderId = order.Id,
                FromStatus = "IN_TRANSIT",
                ToStatus = "ARRIVED",
                CreatedAt = DateTime.UtcNow.AddHours(-arrivedHoursAgo)
            });
            await db.SaveChangesAsync();
            seeded = new Seeded(order.Id, customer.Id, op.Id, dispatcher.Id);
        });
        return seeded!;
    }

    private async Task<int> RunJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPickupTimeoutService>().ExpireOverdueAsync(DateTime.UtcNow);
    }

    private async Task<DeliveryOrder> LoadOrderAsync(Guid id)
    {
        DeliveryOrder? order = null;
        await _factory.WithDbAsync(async db => order = await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == id));
        return order!;
    }

    [Fact]
    public async Task Order_arrived_more_than_24_hours_ago_fails_with_reason_log_audit_and_alerts()
    {
        var s = await SeedArrivedOrderAsync(arrivedHoursAgo: 25);

        await RunJobAsync();

        var order = await LoadOrderAsync(s.OrderId);
        Assert.Equal("FAILED", order.Status);
        Assert.Equal("Pickup window expired (24h timeout)", order.CancelReason);

        await _factory.WithDbAsync(async db =>
        {
            var log = await db.DeliveryStatusLogs.SingleAsync(l => l.OrderId == s.OrderId && l.ToStatus == "FAILED");
            Assert.Equal("ARRIVED", log.FromStatus);
            Assert.Null(log.ChangedBy);
            Assert.Equal("Automated 24h pickup timeout expiration", log.Remarks);

            Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "ORDER_PICKUP_TIMEOUT" && a.EntityId == s.OrderId.ToString()));

            var alerted = await db.Notifications.Where(n => n.OrderId == s.OrderId).Select(n => n.UserId).ToListAsync();
            Assert.Equal(3, alerted.Count);
            Assert.Contains(s.CustomerId, alerted);
            Assert.Contains(s.DispatcherId, alerted);
            Assert.Contains(s.OperatorId, alerted);
        });
    }

    [Fact]
    public async Task Order_arrived_less_than_24_hours_ago_is_left_alone()
    {
        var s = await SeedArrivedOrderAsync(arrivedHoursAgo: 23);

        await RunJobAsync();

        Assert.Equal("ARRIVED", (await LoadOrderAsync(s.OrderId)).Status);
        await _factory.WithDbAsync(async db =>
            Assert.Equal(0, await db.Notifications.CountAsync(n => n.OrderId == s.OrderId)));
    }

    [Theory]
    [InlineData("DELIVERED")]
    [InlineData("FAILED")]
    [InlineData("IN_TRANSIT")]
    public async Task Order_that_is_not_ARRIVED_is_never_touched(string status)
    {
        var s = await SeedArrivedOrderAsync(arrivedHoursAgo: 48, status: status);

        await RunJobAsync();

        var order = await LoadOrderAsync(s.OrderId);
        Assert.Equal(status, order.Status);
        Assert.Null(order.CancelReason);
    }

    [Fact]
    public async Task Running_the_job_twice_fails_the_order_and_alerts_only_once()
    {
        var s = await SeedArrivedOrderAsync(arrivedHoursAgo: 30);

        await RunJobAsync();
        await RunJobAsync();

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == s.OrderId && l.ToStatus == "FAILED"));
            Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "ORDER_PICKUP_TIMEOUT" && a.EntityId == s.OrderId.ToString()));
            Assert.Equal(3, await db.Notifications.CountAsync(n => n.OrderId == s.OrderId));
        });
    }

    [Fact]
    public async Task Order_without_a_dispatcher_alerts_every_active_dispatcher_the_operator_and_the_customer()
    {
        var s = await SeedArrivedOrderAsync(arrivedHoursAgo: 26, withDispatcher: false);

        await RunJobAsync();

        await _factory.WithDbAsync(async db =>
        {
            var alerted = await db.Notifications.Where(n => n.OrderId == s.OrderId).Select(n => n.UserId).ToListAsync();
            Assert.Contains(s.OperatorId, alerted);
            Assert.Contains(s.CustomerId, alerted);
            Assert.Contains(s.DispatcherId, alerted);
            var dispatcherIds = await db.Users.Where(u => u.Role.Code == AppRoles.Dispatcher).Select(u => u.Id).ToListAsync();
            Assert.All(dispatcherIds, id => Assert.Contains(id, alerted));
        });
    }

    [Fact]
    public async Task Order_with_no_ARRIVED_log_is_skipped_instead_of_failing_the_run()
    {
        var s = await SeedArrivedOrderAsync(arrivedHoursAgo: 40);
        await _factory.WithDbAsync(async db =>
        {
            db.DeliveryStatusLogs.RemoveRange(db.DeliveryStatusLogs.Where(l => l.OrderId == s.OrderId));
            await db.SaveChangesAsync();
        });

        await RunJobAsync();

        Assert.Equal("ARRIVED", (await LoadOrderAsync(s.OrderId)).Status);
    }
}
