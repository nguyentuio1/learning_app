using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class SchedulingPostgresTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [PostgresFact]
    public async Task Specification_edit_waiting_for_assignment_rechecks_active_mission_and_refuses_edit()
    {
        using var client = factory.CreateClient();
        var s = await SeedAsync();
        var pause = new PauseSave("DRONE_ASSIGNMENT");
        await using var db = Context(pause);
        var writer = new OrderTransitionWriter(db);
        var service = new SchedulingService(db, writer, new DeliveryRouteEstimate(db, Options.Create(new DeliveryFlightOptions())));
        var assignment = service.AssignAsync(s.Order.Id, s.Actor.Id, new(s.Drone.Id));
        await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var edit = SchedulingData.Send(client, s.Actor.Token, $"/api/drones/{s.Drone.Id}", HttpMethod.Put, Specs(100));
        var blocked = await WaitForLockAsync(edit, "drones");
        pause.Release.TrySetResult();
        await assignment;
        using var response = await edit;
        Assert.True(blocked, "Specification editing must serialize on the drone row.");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await factory.WithDbAsync(async saved => {
            var drone = (await saved.Drones.FindAsync(s.Drone.Id))!;
            Assert.Equal(3000, drone.MaxPayloadGram); Assert.Equal("ASSIGNED", drone.Status);
        });
    }

    [PostgresFact]
    public async Task Assignment_waiting_for_specification_edit_rechecks_new_payload()
    {
        using var client = factory.CreateClient();
        var s = await SeedAsync();
        var pause = new PauseSave("DRONE_UPDATE");
        await using var db = Context(pause);
        var update = new DroneService(db).UpdateAsync(s.Actor.Id, s.Drone.Id, new() { ModelName = "Small", MaxPayloadGram = 100, MaxFlightRangeKm = 15, BatteryCapacityMah = 5000 });
        await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var assignment = SchedulingData.Send(client, s.Actor.Token, $"/api/delivery-orders/{s.Order.Id}/flight-mission", HttpMethod.Post, new { droneId = s.Drone.Id });
        var blocked = await WaitForLockAsync(assignment, "drones");
        pause.Release.TrySetResult();
        await update;
        using var response = await assignment;
        Assert.True(blocked, "Assignment must wait for the specification transaction.");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUnassignedAsync(s.Order.Id, s.Drone.Id);
    }

    [PostgresFact]
    public async Task Package_edit_waiting_for_approval_and_assignment_rechecks_pending_status()
    {
        using var client = factory.CreateClient();
        var s = await SeedAsync("PENDING");
        var customer = await ReviewData.TokenAsync(factory.Services, s.Order.CustomerId);
        await using var db = Context();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM delivery_orders WHERE \"Id\" = {s.Order.Id} FOR UPDATE");
        var order = (await db.DeliveryOrders.FindAsync(s.Order.Id))!;
        order.Status = "APPROVED";
        order.ScheduledDepartureTime = DateTime.UtcNow.AddHours(2);
        order.EstimatedDeliveryTime = order.ScheduledDepartureTime.Value.AddMinutes(2);
        var slot = await db.StationSlots.FirstAsync(x => x.StationId == order.DestinationStationId);
        slot.Status = "RESERVED";
        (await db.Drones.FindAsync(s.Drone.Id))!.Status = "ASSIGNED";
        db.FlightMissions.Add(new FlightMission { OrderId = order.Id, DroneId = s.Drone.Id, DestinationSlotId = slot.Id });
        await db.SaveChangesAsync();
        var edit = SchedulingData.Send(client, customer, $"/api/packages/{s.Order.PackageId}", HttpMethod.Put,
            new { packageName = "Heavier", weightGram = 3000, lengthCm = 25, widthCm = 18, heightCm = 10 });
        var blocked = await WaitForLockAsync(edit, "delivery_orders");
        await tx.CommitAsync();
        using var response = await edit;
        Assert.True(blocked, "A linked package edit must lock and reload the order before checking PENDING.");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await factory.WithDbAsync(async saved => Assert.Equal(450, (await saved.Packages.FindAsync(s.Order.PackageId))!.WeightGram));
    }

    [PostgresFact]
    public async Task Competing_assignments_of_one_order_create_one_mission_and_one_audit()
    {
        using var client = factory.CreateClient();
        var s = await SeedAsync();
        var responses = await TogetherAsync(Enumerable.Range(0, 6).Select(_ => (Func<Task<HttpResponseMessage>>)(() =>
            SchedulingData.Send(client, s.Actor.Token, $"/api/delivery-orders/{s.Order.Id}/flight-mission", HttpMethod.Post, new { droneId = s.Drone.Id }))).ToArray());
        try {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
            await factory.WithDbAsync(async db => {
                Assert.Equal(1, await db.FlightMissions.CountAsync(m => m.OrderId == s.Order.Id));
                Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityId == s.Order.Id.ToString()));
                Assert.Equal(1, await db.StationSlots.CountAsync(x => x.StationId == s.Order.DestinationStationId && x.Status == "RESERVED"));
            });
        } finally { foreach (var r in responses) r.Dispose(); }
    }

    [PostgresFact]
    public async Task Different_orders_cannot_double_book_one_drone_or_one_destination_slot()
    {
        using var client = factory.CreateClient();
        foreach (var sharedDrone in new[] { true, false })
        {
            var s = await SeedAsync();
            var other = await ReviewData.OrderAsync(factory.Services, "APPROVED", s.Order.OriginStationId, s.Order.DestinationStationId);
            var secondDrone = new Drone { SerialNumber = Guid.NewGuid().ToString("N"), ModelName = "Other", MaxPayloadGram = 3000, MaxFlightRangeKm = 15, CurrentStationId = other.OriginStationId };
            await factory.WithDbAsync(async db => {
                var o = (await db.DeliveryOrders.FindAsync(other.Id))!;
                o.ScheduledDepartureTime = s.Order.ScheduledDepartureTime; o.EstimatedDeliveryTime = s.Order.EstimatedDeliveryTime;
                db.Drones.Add(secondDrone);
                if (!sharedDrone) db.StationSlots.RemoveRange(await db.StationSlots.Where(x => x.StationId == other.DestinationStationId && x.SlotNumber == 2).ToListAsync());
                await db.SaveChangesAsync();
            });
            var responses = await TogetherAsync(
                () => SchedulingData.Send(client, s.Actor.Token, $"/api/delivery-orders/{s.Order.Id}/flight-mission", HttpMethod.Post, new { droneId = s.Drone.Id }),
                () => SchedulingData.Send(client, s.Actor.Token, $"/api/delivery-orders/{other.Id}/flight-mission", HttpMethod.Post, new { droneId = sharedDrone ? s.Drone.Id : secondDrone.Id }));
            try {
                Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
                Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
                await factory.WithDbAsync(async db => Assert.Equal(1, await db.FlightMissions.CountAsync(m => m.OrderId == s.Order.Id || m.OrderId == other.Id)));
            } finally { foreach (var r in responses) r.Dispose(); }
        }
    }

    [PostgresFact]
    public async Task Schedule_and_assignment_race_produce_one_immutable_mission_schedule()
    {
        using var client = factory.CreateClient();
        var s = await SeedAsync();
        var responses = await TogetherAsync(
            () => SchedulingData.Send(client, s.Actor.Token, $"/api/delivery-orders/{s.Order.Id}/schedule", HttpMethod.Put, new { scheduledDepartureTime = DateTime.UtcNow.AddHours(4) }),
            () => SchedulingData.Send(client, s.Actor.Token, $"/api/delivery-orders/{s.Order.Id}/flight-mission", HttpMethod.Post, new { droneId = s.Drone.Id }));
        try {
            Assert.Contains(responses[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            Assert.Equal(HttpStatusCode.Created, responses[1].StatusCode);
            await factory.WithDbAsync(async db => {
                var order = (await db.DeliveryOrders.FindAsync(s.Order.Id))!;
                var mission = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id);
                Assert.Equal(order.ScheduledDepartureTime, mission.ScheduledDepartureTime);
                Assert.Equal(order.EstimatedDeliveryTime, mission.EstimatedArrivalTime);
            });
        } finally { foreach (var r in responses) r.Dispose(); }
    }

    [PostgresFact]
    public async Task Cancellation_racing_assignment_never_leaves_resources_assigned_to_cancelled_order()
    {
        using var client = factory.CreateClient();
        var s = await SeedAsync();
        var customer = await ReviewData.TokenAsync(factory.Services, s.Order.CustomerId);
        var responses = await TogetherAsync(
            () => SchedulingData.Send(client, s.Actor.Token, $"/api/delivery-orders/{s.Order.Id}/flight-mission", HttpMethod.Post, new { droneId = s.Drone.Id }),
            () => SchedulingData.Send(client, customer, $"/api/delivery-orders/{s.Order.Id}/cancel", HttpMethod.Post, new { cancellationReason = "Changed plans" }));
        try {
            Assert.Contains(responses[0].StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
            Assert.Equal(HttpStatusCode.OK, responses[1].StatusCode);
            await AssertUnassignedAsync(s.Order.Id, s.Drone.Id);
            await factory.WithDbAsync(async db => Assert.Equal("CANCELLED", (await db.DeliveryOrders.FindAsync(s.Order.Id))!.Status));
        } finally { foreach (var r in responses) r.Dispose(); }
    }

    [PostgresFact]
    public async Task Assignment_audit_failure_rolls_back_mission_drone_slot_and_order_update()
    {
        using var client = factory.CreateClient();
        var s = await SeedAsync();
        var originalUpdate = s.Order.UpdatedAt;
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
        var name = "assignment_fail_" + Guid.NewGuid().ToString("N");
        await using (var command = new NpgsqlCommand($"""
            CREATE FUNCTION {name}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW."EntityId" = '{s.Order.Id}' THEN RAISE EXCEPTION 'Injected assignment failure'; END IF; RETURN NEW;
            END; $$;
            CREATE TRIGGER {name} BEFORE INSERT ON audit_logs FOR EACH ROW EXECUTE FUNCTION {name}();
            """, connection)) await command.ExecuteNonQueryAsync();
        try {
            using var response = await SchedulingData.Send(client, s.Actor.Token, $"/api/delivery-orders/{s.Order.Id}/flight-mission", HttpMethod.Post, new { droneId = s.Drone.Id });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            await AssertUnassignedAsync(s.Order.Id, s.Drone.Id);
            await factory.WithDbAsync(async db => {
                Assert.False(await db.FlightMissions.AnyAsync(m => m.OrderId == s.Order.Id));
                Assert.Equal(originalUpdate, (await db.DeliveryOrders.FindAsync(s.Order.Id))!.UpdatedAt);
            });
        } finally {
            await using var command = new NpgsqlCommand($"DROP TRIGGER {name} ON audit_logs; DROP FUNCTION {name}();", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    [PostgresFact]
    public async Task Partial_active_index_allows_history_but_rejects_second_active_mission()
    {
        var s = await SeedAsync();
        await using var db = Context();
        db.FlightMissions.AddRange(new FlightMission { OrderId = s.Order.Id, DroneId = s.Drone.Id, FlightStatus = "ABORTED" },
            new FlightMission { OrderId = s.Order.Id, DroneId = s.Drone.Id, FlightStatus = "COMPLETED" },
            new FlightMission { OrderId = s.Order.Id, DroneId = s.Drone.Id });
        await db.SaveChangesAsync();
        db.FlightMissions.Add(new FlightMission { OrderId = s.Order.Id, DroneId = s.Drone.Id, FlightStatus = "IN_AIR" });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("IX_flight_missions_OrderId_Active", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
    }

    [PostgresFact]
    public async Task Drone_and_package_edits_roll_back_on_database_failure_without_storage_cleanup()
    {
        using var client = factory.CreateClient();
        var s = await SeedAsync("PENDING");
        var customer = await ReviewData.TokenAsync(factory.Services, s.Order.CustomerId);
        var oldImage = $"https://storage.example/{s.Order.PackageId:D}.png";
        await factory.WithDbAsync(async db => { (await db.Packages.FindAsync(s.Order.PackageId))!.PackageImageUrl = oldImage; await db.SaveChangesAsync(); });
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
        var name = "edit_fail_" + Guid.NewGuid().ToString("N");
        await using (var command = new NpgsqlCommand($"""
            CREATE FUNCTION {name}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW."Id" IN ('{s.Drone.Id}'::uuid, '{s.Order.PackageId}'::uuid) THEN RAISE EXCEPTION 'Injected edit failure'; END IF; RETURN NEW;
            END; $$;
            CREATE TRIGGER {name} BEFORE UPDATE ON drones FOR EACH ROW EXECUTE FUNCTION {name}();
            CREATE TRIGGER {name} BEFORE UPDATE ON packages FOR EACH ROW EXECUTE FUNCTION {name}();
            """, connection)) await command.ExecuteNonQueryAsync();
        try {
            using var drone = await SchedulingData.Send(client, s.Actor.Token, $"/api/drones/{s.Drone.Id}", HttpMethod.Put, Specs(100));
            using var package = await SchedulingData.Send(client, customer, $"/api/packages/{s.Order.PackageId}", HttpMethod.Put,
                new { packageName = "Heavier", weightGram = 2500, lengthCm = 25, widthCm = 18, heightCm = 10, packageImageUrl = "https://storage.example/new.png" });
            Assert.Equal(HttpStatusCode.InternalServerError, drone.StatusCode);
            Assert.Equal(HttpStatusCode.InternalServerError, package.StatusCode);
            await factory.WithDbAsync(async db => {
                Assert.Equal(3000, (await db.Drones.FindAsync(s.Drone.Id))!.MaxPayloadGram);
                var p = (await db.Packages.FindAsync(s.Order.PackageId))!;
                Assert.Equal(450, p.WeightGram); Assert.Equal(oldImage, p.PackageImageUrl);
                Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityId == s.Drone.Id.ToString()));
            });
            Assert.DoesNotContain(oldImage, factory.Packages.Deleted);
        } finally {
            await using var command = new NpgsqlCommand($"DROP TRIGGER {name} ON drones; DROP TRIGGER {name} ON packages; DROP FUNCTION {name}();", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    [PostgresFact]
    public async Task Cancellation_notification_failure_restores_planned_assignment_and_reservation()
    {
        using var client = factory.CreateClient();
        var s = await SeedAsync();
        using var assignment = await SchedulingData.Send(client, s.Actor.Token, $"/api/delivery-orders/{s.Order.Id}/flight-mission", HttpMethod.Post, new { droneId = s.Drone.Id });
        Assert.Equal(HttpStatusCode.Created, assignment.StatusCode);
        var customer = await ReviewData.TokenAsync(factory.Services, s.Order.CustomerId);
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
        var name = "cancel_fail_" + Guid.NewGuid().ToString("N");
        await using (var command = new NpgsqlCommand($"""
            CREATE FUNCTION {name}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW."OrderId" = '{s.Order.Id}'::uuid THEN RAISE EXCEPTION 'Injected cancellation failure'; END IF; RETURN NEW;
            END; $$;
            CREATE TRIGGER {name} BEFORE INSERT ON notifications FOR EACH ROW EXECUTE FUNCTION {name}();
            """, connection)) await command.ExecuteNonQueryAsync();
        try {
            using var response = await SchedulingData.Send(client, customer, $"/api/delivery-orders/{s.Order.Id}/cancel", HttpMethod.Post, new { cancellationReason = "Changed plans" });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            await factory.WithDbAsync(async db => {
                var mission = await db.FlightMissions.SingleAsync(m => m.OrderId == s.Order.Id);
                Assert.Equal("PLANNED", mission.FlightStatus); Assert.Null(mission.EndTime);
                Assert.Equal("ASSIGNED", (await db.Drones.FindAsync(s.Drone.Id))!.Status);
                Assert.Equal("RESERVED", (await db.StationSlots.FindAsync(mission.DestinationSlotId))!.Status);
                Assert.Equal("APPROVED", (await db.DeliveryOrders.FindAsync(s.Order.Id))!.Status);
                Assert.False(await db.DeliveryStatusLogs.AnyAsync(l => l.OrderId == s.Order.Id));
            });
        } finally {
            await using var command = new NpgsqlCommand($"DROP TRIGGER {name} ON notifications; DROP FUNCTION {name}();", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    [PostgresFact]
    public async Task Assignment_waits_for_station_status_edit_and_rechecks_operational_status()
    {
        using var client = factory.CreateClient();
        var s = await SeedAsync();
        await using var db = Context();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE landing_stations SET \"Status\" = 'MAINTENANCE' WHERE \"Id\" = {s.Order.DestinationStationId}");
        var assignment = SchedulingData.Send(client, s.Actor.Token, $"/api/delivery-orders/{s.Order.Id}/flight-mission", HttpMethod.Post, new { droneId = s.Drone.Id });
        var blocked = await WaitForLockAsync(assignment, "landing_stations");
        await tx.CommitAsync();
        using var response = await assignment;
        Assert.True(blocked);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertUnassignedAsync(s.Order.Id, s.Drone.Id);
    }

    [PostgresFact]
    public async Task Intake_station_lock_prevents_prechecked_empty_slot_deletion_after_intake_commits()
    {
        using var client = factory.CreateClient();
        var s = await SeedAsync();
        var admin = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var source = new StationSlot { StationId = s.Order.OriginStationId, SlotNumber = 1 };
        await factory.WithDbAsync(async db => { db.StationSlots.AddRange(source, new StationSlot { StationId = source.StationId, SlotNumber = 2 }); await db.SaveChangesAsync(); });
        using var assignment = await SchedulingData.Send(client, s.Actor.Token, $"/api/delivery-orders/{s.Order.Id}/flight-mission", HttpMethod.Post, new { droneId = s.Drone.Id });
        Assert.Equal(HttpStatusCode.Created, assignment.StatusCode);
        var pause = new PauseSave("PACKAGE_INTAKE_CONFIRMATION");
        await using var context = Context(pause);
        var intake = new StationOperationService(context, new OrderTransitionWriter(context)).IntakeAsync(admin.Id, source.StationId,
            new() { TrackingNumber = s.Order.TrackingNumber, SlotId = source.Id, MeasuredWeightGram = 450, MeasuredLengthCm = 25, MeasuredWidthCm = 18, MeasuredHeightCm = 10, VisualInspectionPassed = true });
        await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var deletion = SchedulingData.Send(client, admin.Token, $"/api/station-slots/{source.Id}", HttpMethod.Delete);
        var blocked = await WaitForLockAsync(deletion, "landing_stations");
        pause.Release.TrySetResult();
        await intake;
        using var response = await deletion;
        Assert.True(blocked, "Slot deletion must wait on the station before it checks EMPTY.");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await factory.WithDbAsync(async db => Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(source.Id))!.Status));
    }

    private static object Specs(decimal weight) => new { modelName = "Small", maxPayloadGram = weight, maxFlightRangeKm = 15, batteryCapacityMah = 5000 };

    private async Task<(ReviewData.Actor Actor, DeliveryOrder Order, Drone Drone)> SeedAsync(string status = "APPROVED")
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services, status);
        var drone = await SchedulingData.ResourcesAsync(factory.Services, order);
        await factory.WithDbAsync(async db => {
            var o = (await db.DeliveryOrders.FindAsync(order.Id))!;
            o.ScheduledDepartureTime = DateTime.UtcNow.AddHours(2); o.EstimatedDeliveryTime = o.ScheduledDepartureTime.Value.AddMinutes(2);
            await db.SaveChangesAsync();
            order.ScheduledDepartureTime = o.ScheduledDepartureTime; order.EstimatedDeliveryTime = o.EstimatedDeliveryTime;
            order.UpdatedAt = o.UpdatedAt;
        });
        return (actor, order, drone);
    }

    private AppDbContext Context(IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(factory.DatabaseConnectionString);
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options);
    }

    private async Task AssertUnassignedAsync(Guid orderId, Guid droneId) => await factory.WithDbAsync(async db => {
        Assert.False(await db.FlightMissions.AnyAsync(m => m.OrderId == orderId && (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR")));
        Assert.Equal("IDLE", (await db.Drones.FindAsync(droneId))!.Status);
        var order = (await db.DeliveryOrders.FindAsync(orderId))!;
        Assert.All(await db.StationSlots.Where(s => s.StationId == order.DestinationStationId).ToListAsync(), s => Assert.Equal("EMPTY", s.Status));
    });

    private async Task<bool> WaitForLockAsync(Task pending, string table)
    {
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
        for (var i = 0; i < 150 && !pending.IsCompleted; i++) {
            await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE @pattern)", connection);
            command.Parameters.AddWithValue("pattern", "%" + table + "%");
            if ((bool)(await command.ExecuteScalarAsync())!) return true;
            await Task.Delay(20);
        }
        return false;
    }

    private static async Task<HttpResponseMessage[]> TogetherAsync(params Func<Task<HttpResponseMessage>>[] calls)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = calls.Select(async call => { await gate.Task; return await call(); }).ToArray();
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    private sealed class PauseSave(string action) : SaveChangesInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.Entity.Action == action && e.State == EntityState.Added)) {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
            return result;
        }
    }
}
