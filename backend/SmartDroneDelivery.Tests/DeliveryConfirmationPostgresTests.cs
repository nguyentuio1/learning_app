using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class DeliveryConfirmationPostgresTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private record Scenario(Guid Order, Guid Owner, Guid Operator, Guid Station, Guid Slot, string Tracking);

    [PostgresFact]
    public async Task Customer_and_operator_racing_complete_the_order_exactly_once()
    {
        var data = await SeedAsync();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Submit(bool customer) { await start.Task; return await SendAsync(data, customer, "593284"); }
        var first = Submit(true); var second = Submit(false); start.SetResult();
        var results = await Task.WhenAll(first, second);
        try {
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
            await factory.WithDbAsync(async db => {
                Assert.Equal("DELIVERED", (await db.DeliveryOrders.FindAsync(data.Order))!.Status);
                Assert.NotNull((await db.DeliveryOrders.FindAsync(data.Order))!.ActualDeliveryTime);
                Assert.Equal("EMPTY", (await db.StationSlots.FindAsync(data.Slot))!.Status);
                Assert.Equal(1, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == data.Order && l.ToStatus == "DELIVERED"));
                Assert.Equal(1, await db.Notifications.CountAsync(n => n.OrderId == data.Order));
                Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityId == data.Order.ToString() && a.Action == "PACKAGE_DELIVERY_CONFIRMATION"));
            });
        }
        finally { foreach (var result in results) result.Dispose(); }
    }

    [PostgresFact]
    public async Task Parallel_customer_and_operator_wrong_codes_share_the_same_three_attempt_budget()
    {
        var data = await SeedAsync();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Submit(bool customer) { await start.Task; return await SendAsync(data, customer, "000000"); }
        var first = Submit(true); var second = Submit(false); var third = Submit(true); start.SetResult();
        var results = await Task.WhenAll(first, second, third);
        try {
            Assert.Equal(2, results.Count(r => r.StatusCode == HttpStatusCode.BadRequest));
            Assert.Single(results, r => r.StatusCode == (HttpStatusCode)423);
            using var correct = await SendAsync(data, true, "593284");
            Assert.Equal((HttpStatusCode)423, correct.StatusCode);
            await factory.WithDbAsync(async db => {
                Assert.Equal(3, await db.AuditLogs.CountAsync(a => a.EntityId == data.Order.ToString() && a.Action == "PICKUP_OTP_FAILED"));
                Assert.Equal("ARRIVED", (await db.DeliveryOrders.FindAsync(data.Order))!.Status);
                Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(data.Slot))!.Status);
            });
        }
        finally { foreach (var result in results) result.Dispose(); }
    }

    [PostgresFact]
    public async Task Failed_confirmation_audit_rolls_back_status_timestamp_slot_logs_notification_and_counter_reset()
    {
        var data = await SeedAsync();
        await factory.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_la150_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW."Action" = 'PACKAGE_DELIVERY_CONFIRMATION' THEN
                    RAISE EXCEPTION 'Injected confirmation audit failure';
                END IF; RETURN NEW; END; $$;
            CREATE TRIGGER reject_la150_audit BEFORE INSERT ON audit_logs FOR EACH ROW EXECUTE FUNCTION reject_la150_audit();
            """));
        try {
            using var response = await SendAsync(data, true, "593284");
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            await factory.WithDbAsync(async db => {
                var order = (await db.DeliveryOrders.FindAsync(data.Order))!;
                Assert.Equal("ARRIVED", order.Status);
                Assert.Null(order.ActualDeliveryTime);
                Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(data.Slot))!.Status);
                Assert.Empty(await db.DeliveryStatusLogs.Where(l => l.OrderId == data.Order).ToListAsync());
                Assert.Empty(await db.Notifications.Where(n => n.OrderId == data.Order).ToListAsync());
                Assert.Empty(await db.AuditLogs.Where(a => a.EntityId == data.Order.ToString()).ToListAsync());
            });
        }
        finally {
            await factory.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_la150_audit ON audit_logs; DROP FUNCTION reject_la150_audit();"));
        }
        using var retry = await SendAsync(data, true, "593284");
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [PostgresFact]
    public Task Ownership_is_rechecked_after_waiting_for_the_order_lock() => AssertScopeRecheckedAsync("owner");

    [PostgresFact]
    public Task Account_deactivation_is_rechecked_after_waiting_for_the_order_lock() => AssertScopeRecheckedAsync("inactive");

    [PostgresFact]
    public Task Customer_role_is_rechecked_after_waiting_for_the_order_lock() => AssertScopeRecheckedAsync("role");

    private async Task AssertScopeRecheckedAsync(string change)
    {
        var data = await SeedAsync();
        await using var locker = new NpgsqlConnection(factory.DatabaseConnectionString);
        await locker.OpenAsync();
        await using var transaction = await locker.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT \"Id\" FROM delivery_orders WHERE \"Id\" = @id FOR UPDATE", locker, transaction)) {
            command.Parameters.AddWithValue("id", data.Order); await command.ExecuteScalarAsync();
        }
        var pending = SendAsync(data, true, "593284");
        await using var observer = new NpgsqlConnection(factory.DatabaseConnectionString);
        await observer.OpenAsync();
        var waiting = false;
        for (var attempt = 0; attempt < 200; attempt++) {
            await using var query = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = @db AND wait_event_type = 'Lock' AND query LIKE '%delivery_orders%')", observer);
            query.Parameters.AddWithValue("db", factory.DatabaseName);
            if (await query.ExecuteScalarAsync() is true) { waiting = true; break; }
            await Task.Delay(25);
        }
        if (change == "owner") {
            Guid nextOwner = default;
            await factory.WithDbAsync(async db => {
                var replacement = new User { RoleId = (await db.Roles.SingleAsync(r => r.Code == "CUSTOMER")).Id,
                    Email = $"replacement-owner-{Guid.NewGuid():N}@example.com", PhoneNumber = Guid.NewGuid().ToString("N")[..15], Status = "ACTIVE" };
                db.Users.Add(replacement); await db.SaveChangesAsync(); nextOwner = replacement.Id;
            });
            await using var update = new NpgsqlCommand("UPDATE delivery_orders SET \"CustomerId\" = @owner WHERE \"Id\" = @id", locker, transaction);
            update.Parameters.AddWithValue("owner", nextOwner); update.Parameters.AddWithValue("id", data.Order);
            await update.ExecuteNonQueryAsync();
        }
        else await factory.WithDbAsync(async db => {
            var user = (await db.Users.FindAsync(data.Owner))!;
            if (change == "inactive") user.Status = "INACTIVE";
            else user.RoleId = (await db.Roles.SingleAsync(r => r.Code == "ADMIN")).Id;
            await db.SaveChangesAsync();
        });
        await transaction.CommitAsync();
        using var result = await pending.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(waiting, "The request must really wait on the order row lock before its access is changed.");
        Assert.Equal(change == "owner" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, result.StatusCode);
        await factory.WithDbAsync(async db => {
            Assert.Equal("ARRIVED", (await db.DeliveryOrders.FindAsync(data.Order))!.Status);
            Assert.Empty(await db.AuditLogs.Where(a => a.EntityId == data.Order.ToString()).ToListAsync());
        });
    }

    private async Task<Scenario> SeedAsync()
    {
        Scenario result = null!;
        await factory.WithDbAsync(async db => {
            var stationOperator = new User { RoleId = (await db.Roles.SingleAsync(r => r.Code == "STATION_OPERATOR")).Id,
                Email = $"confirmation-op-{Guid.NewGuid():N}@example.com", PhoneNumber = Guid.NewGuid().ToString("N")[..15], Status = "ACTIVE" };
            db.Users.Add(stationOperator);
            var missionId = await TelemetryTestData.SeedMissionAsync(db, "COMPLETED");
            var mission = await db.FlightMissions.Include(m => m.Order).ThenInclude(o => o.DestinationStation).SingleAsync(m => m.Id == missionId);
            mission.Order.Status = "ARRIVED";
            mission.Order.DestinationStation.OperatorId = stationOperator.Id;
            var slot = new StationSlot { StationId = mission.Order.DestinationStationId, SlotNumber = 1, Status = "OCCUPIED" };
            db.StationSlots.Add(slot); mission.DestinationSlot = slot;
            await db.SaveChangesAsync();
            result = new(mission.OrderId, mission.Order.CustomerId, stationOperator.Id, slot.StationId, slot.Id, mission.Order.TrackingNumber);
        });
        return result;
    }

    private async Task<HttpResponseMessage> SendAsync(Scenario data, bool customer, string code)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.Include(u => u.Role).SingleAsync(u => u.Id == (customer ? data.Owner : data.Operator));
        var token = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, DateTime.UtcNow);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return customer
            ? await client.PostAsJsonAsync($"/api/delivery-orders/{data.Order}/confirm-delivery", new { pickupSecureCode = code })
            : await client.PostAsJsonAsync($"/api/landing-stations/{data.Station}/operations/pickup", new { trackingNumber = data.Tracking, pickupSecureCode = code });
    }
}
