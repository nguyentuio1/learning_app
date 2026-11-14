using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class OrderStateMachineTests
{
    [Fact]
    public void Transition_writer_is_registered_for_workflow_services()
    {
        using var factory = new AuthApiFactory();
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOrderTransitionWriter>());
    }

    [Theory]
    [InlineData("PENDING", "APPROVED")]
    [InlineData("PENDING", "CANCELLED")]
    [InlineData("APPROVED", "CANCELLED")]
    [InlineData("APPROVED", "READY_FOR_TAKEOFF")]
    [InlineData("APPROVED", "FAILED")]
    [InlineData("READY_FOR_TAKEOFF", "IN_TRANSIT")]
    [InlineData("READY_FOR_TAKEOFF", "CANCELLED")]
    [InlineData("IN_TRANSIT", "ARRIVED")]
    [InlineData("IN_TRANSIT", "FAILED")]
    [InlineData("ARRIVED", "DELIVERED")]
    [InlineData("ARRIVED", "FAILED")]
    [InlineData("FAILED", "APPROVED")]
    [InlineData("FAILED", "CANCELLED")]
    public async Task Allowed_transition_records_original_status_actor_and_utc_time(string from, string to)
    {
        await using var db = NewDb();
        var order = new DeliveryOrder { Status = from, TrackingNumber = "DD-202611-0001" };
        db.DeliveryOrders.Add(order);
        await db.SaveChangesAsync();
        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await new OrderTransitionWriter(db).StageAsync(order, from, to, actor, "Checked", now);
        await db.SaveChangesAsync();

        var log = Assert.Single(await db.DeliveryStatusLogs.ToListAsync());
        Assert.Equal(from, log.FromStatus);
        Assert.Equal(to, log.ToStatus);
        Assert.Equal(actor, log.ChangedBy);
        Assert.Equal(now, log.CreatedAt);
        Assert.Equal("Checked", log.Remarks);
        Assert.Equal(to, order.Status);
        Assert.Equal(now, order.UpdatedAt);
    }

    [Fact]
    public async Task All_other_arrows_including_terminal_and_unknown_states_are_refused_without_changes()
    {
        string[] states = ["PENDING", "APPROVED", "READY_FOR_TAKEOFF", "IN_TRANSIT", "ARRIVED", "DELIVERED", "CANCELLED", "FAILED", "SCHEDULED"];
        var allowed = new HashSet<(string, string)>
        {
            ("PENDING", "APPROVED"), ("PENDING", "CANCELLED"),
            ("APPROVED", "CANCELLED"), ("APPROVED", "READY_FOR_TAKEOFF"), ("APPROVED", "FAILED"),
            ("READY_FOR_TAKEOFF", "IN_TRANSIT"), ("READY_FOR_TAKEOFF", "CANCELLED"),
            ("IN_TRANSIT", "ARRIVED"), ("IN_TRANSIT", "FAILED"),
            ("ARRIVED", "DELIVERED"), ("ARRIVED", "FAILED"),
            ("FAILED", "APPROVED"), ("FAILED", "CANCELLED")
        };
        foreach (var from in states)
        foreach (var to in states)
        {
            if (allowed.Contains((from, to))) continue;
            await using var db = NewDb();
            var order = new DeliveryOrder { Status = from };
            db.Add(order);
            await db.SaveChangesAsync();
            var before = order.UpdatedAt;

            var error = await Assert.ThrowsAsync<ApiException>(() =>
                new OrderTransitionWriter(db).StageAsync(order, from, to, null, null, DateTime.UtcNow));

            Assert.Equal(409, error.StatusCode);
            Assert.Equal(from, order.Status);
            Assert.Equal(before, order.UpdatedAt);
            Assert.Empty(db.ChangeTracker.Entries<DeliveryStatusLog>());
            Assert.Empty(await db.Notifications.ToListAsync());
        }
    }

    [Fact]
    public async Task Stale_expected_status_is_refused_without_log_or_notification()
    {
        await using var db = NewDb();
        var order = new DeliveryOrder { Status = "CANCELLED" };
        db.Add(order);
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<ApiException>(() => new OrderTransitionWriter(db)
            .StageAsync(order, "PENDING", "APPROVED", Guid.NewGuid(), null, DateTime.UtcNow));
        Assert.Equal(409, error.StatusCode);
        Assert.Empty(db.ChangeTracker.Entries<DeliveryStatusLog>());
        Assert.Empty(db.ChangeTracker.Entries<Notification>());
    }

    [Fact]
    public async Task Standalone_writer_notifies_customer_once_and_repeated_request_conflicts()
    {
        await using var db = NewDb();
        var order = new DeliveryOrder { Status = "PENDING", TrackingNumber = "DD-202611-0002", CustomerId = Guid.NewGuid(), PickupSecureCode = "593284" };
        db.Add(order);
        await db.SaveChangesAsync();
        var writer = new OrderTransitionWriter(db);
        await writer.ApplyAsync(order.Id, "PENDING", "APPROVED", null, "Automatic approval");

        var log = Assert.Single(await db.DeliveryStatusLogs.ToListAsync());
        Assert.Null(log.ChangedBy);
        var notice = Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal(order.CustomerId, notice.UserId);
        Assert.Equal(order.Id, notice.OrderId);
        Assert.DoesNotContain(order.PickupSecureCode, notice.Message);
        var error = await Assert.ThrowsAsync<ApiException>(() => writer.ApplyAsync(order.Id, "PENDING", "APPROVED", null, null));
        Assert.Equal(409, error.StatusCode);
        Assert.Equal(1, await db.DeliveryStatusLogs.CountAsync());
        Assert.Equal(1, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task ApplyAsync_nonexistent_order_throws_404_and_leaves_no_records()
    {
        await using var db = NewDb();
        var writer = new OrderTransitionWriter(db);
        var nonexistentId = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            writer.ApplyAsync(nonexistentId, "PENDING", "APPROVED", null, null));

        Assert.Equal(404, error.StatusCode);
        Assert.Equal("Delivery order not found.", error.Message);
        Assert.Empty(await db.DeliveryStatusLogs.ToListAsync());
        Assert.Empty(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task StageAsync_untracked_entity_throws_InvalidOperationException()
    {
        await using var db = NewDb();
        var untrackedOrder = new DeliveryOrder { Id = Guid.NewGuid(), Status = "PENDING" };
        var writer = new OrderTransitionWriter(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            writer.StageAsync(untrackedOrder, "PENDING", "APPROVED", null, null, DateTime.UtcNow));

        Assert.Contains("requires a tracked, persisted order", ex.Message);
    }

    [Theory]
    [InlineData("DELIVERED")]
    [InlineData("CANCELLED")]
    public async Task Terminal_states_strictly_forbid_any_outgoing_transitions(string terminalStatus)
    {
        string[] allPossibleStates = ["PENDING", "APPROVED", "READY_FOR_TAKEOFF", "IN_TRANSIT", "ARRIVED", "DELIVERED", "CANCELLED", "FAILED"];
        foreach (var target in allPossibleStates)
        {
            Assert.False(OrderStateMachine.CanTransition(terminalStatus, target),
                $"Terminal status {terminalStatus} should never transition to {target}");

            await using var db = NewDb();
            var order = new DeliveryOrder { Status = terminalStatus, TrackingNumber = "DD-202611-TERM" };
            db.Add(order);
            await db.SaveChangesAsync();

            var writer = new OrderTransitionWriter(db);
            var error = await Assert.ThrowsAsync<ApiException>(() =>
                writer.StageAsync(order, terminalStatus, target, null, null, DateTime.UtcNow));

            Assert.Equal(409, error.StatusCode);
            Assert.Equal(terminalStatus, order.Status);
            Assert.Empty(db.ChangeTracker.Entries<DeliveryStatusLog>());
        }
    }

    [Theory]
    [InlineData("SCHEDULED")]
    [InlineData("PICKED_UP")]
    [InlineData("EN_ROUTE")]
    [InlineData("COMPLETED")]
    [InlineData("RETURNED")]
    [InlineData("UNKNOWN")]
    public void CanTransition_strictly_rejects_noncanonical_status_names(string nonCanonicalStatus)
    {
        string[] canonicalStates = ["PENDING", "APPROVED", "READY_FOR_TAKEOFF", "IN_TRANSIT", "ARRIVED", "DELIVERED", "CANCELLED", "FAILED"];
        foreach (var canonical in canonicalStates)
        {
            Assert.False(OrderStateMachine.CanTransition(nonCanonicalStatus, canonical));
            Assert.False(OrderStateMachine.CanTransition(canonical, nonCanonicalStatus));
        }
    }

    [Fact]
    public async Task EnterOperationAsync_allows_clean_sequential_scopes_without_deadlock()
    {
        await using var db = NewDb();
        var writer = new OrderTransitionWriter(db);

        using (var scope1 = await writer.EnterOperationAsync())
        {
            Assert.NotNull(scope1);
        }

        using (var scope2 = await writer.EnterOperationAsync())
        {
            Assert.NotNull(scope2);
        }
    }

    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
