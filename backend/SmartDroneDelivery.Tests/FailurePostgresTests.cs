using System.Net;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class FailurePostgresTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [PostgresFact]
    public async Task Detail_does_not_attribute_a_new_source_parcel_when_intake_commits_before_occupied_read()
    {
        using var client = factory.CreateClient();
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var failed = await FailureData.InAirAsync(factory.Services);
        using var report = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{failed.Id}/fail", HttpMethod.Post, new { failureType = "MISSION_ABORTED", reason = "Stopped" });
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        var replacement = await ReviewData.OrderAsync(factory.Services, "APPROVED", originId: failed.DestinationStationId);
        var drone = await SchedulingData.ResourcesAsync(factory.Services, replacement);
        using var scheduled = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{replacement.Id}/schedule", HttpMethod.Put, new { scheduledDepartureTime = DateTime.UtcNow.AddHours(1) });
        Assert.Equal(HttpStatusCode.OK, scheduled.StatusCode);
        using var assigned = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{replacement.Id}/flight-mission", HttpMethod.Post, new { droneId = drone.Id });
        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        Guid slotId = Guid.Empty;
        await factory.WithDbAsync(async db => {
            var old = await db.FlightMissions.SingleAsync(m => m.OrderId == failed.Id);
            slotId = old.DestinationSlotId!.Value;
            // Fixture models station removal/release; no LA-336 route is introduced.
            (await db.StationSlots.FindAsync(slotId))!.Status = "EMPTY";
            (await db.FlightMissions.SingleAsync(m => m.OrderId == replacement.Id)).CreatedAt = old.CreatedAt.AddMinutes(-10);
            await db.SaveChangesAsync();
        });
        var pause = new PauseOccupiedRead();
        await using var readerDb = Context(pause);
        var reading = new FailureService(readerDb, new OrderTransitionWriter(readerDb), TimeProvider.System).DetailAsync(failed.Id, actor.Id);
        try {
            await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var intake = await SchedulingData.Send(client, actor.Token, $"/api/landing-stations/{replacement.OriginStationId}/operations/intake", HttpMethod.Post,
                new { trackingNumber = replacement.TrackingNumber, slotId, measuredWeightGram = 450, measuredLengthCm = 25, measuredWidthCm = 18, measuredHeightCm = 10, visualInspectionPassed = true });
            Assert.Equal(HttpStatusCode.OK, intake.StatusCode);
            await factory.WithDbAsync(async db => {
                Assert.Equal("READY_FOR_TAKEOFF", (await db.DeliveryOrders.FindAsync(replacement.Id))!.Status);
                Assert.Equal(slotId, (await db.FlightMissions.SingleAsync(m => m.OrderId == replacement.Id)).StartSlotId);
                Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(slotId))!.Status);
            });
        }
        finally { pause.Release.TrySetResult(); }
        var detail = await reading;
        Assert.Null(detail.ParcelLocation);
    }

    [PostgresFact]
    public async Task Report_waiting_for_arrival_rechecks_committed_state_and_does_not_abort_landed_mission()
    {
        using var client = factory.CreateClient();
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await FailureData.InAirAsync(factory.Services);
        await using var db = Context();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM delivery_orders WHERE \"Id\" = {order.Id} FOR UPDATE");
        var saved = (await db.DeliveryOrders.FindAsync(order.Id))!;
        var now = DateTime.UtcNow;
        await new OrderTransitionWriter(db).StageAsync(saved, "IN_TRANSIT", "ARRIVED", null, "Arrival won", now);
        var mission = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id); mission.FlightStatus = "COMPLETED"; mission.EndTime = now;
        var drone = (await db.Drones.FindAsync(mission.DroneId))!; drone.Status = "CHARGING"; drone.CurrentStationId = order.DestinationStationId;
        (await db.StationSlots.FindAsync(mission.DestinationSlotId))!.Status = "OCCUPIED";
        await db.SaveChangesAsync();
        var pending = SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = "MISSION_ABORTED", reason = "Too late" });
        var waited = await WaitForOrderLockAsync(pending);
        await tx.CommitAsync();
        using var response = await pending;
        Assert.True(waited, "Failure reporting must wait on the order transaction.");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await factory.WithDbAsync(async outside => {
            Assert.Equal("ARRIVED", (await outside.DeliveryOrders.FindAsync(order.Id))!.Status);
            Assert.Equal("COMPLETED", (await outside.FlightMissions.FindAsync(mission.Id))!.FlightStatus);
            Assert.Equal("CHARGING", (await outside.Drones.FindAsync(mission.DroneId))!.Status);
            Assert.Equal(0, await outside.DeliveryFailureEpisodes.CountAsync(e => e.FailureLog.OrderId == order.Id));
        });
    }

    [PostgresFact]
    public async Task Timeout_metadata_and_effects_roll_back_together_after_successful_save_throws()
    {
        _ = factory.CreateClient();
        var order = await ReviewData.OrderAsync(factory.Services, "ARRIVED");
        await factory.WithDbAsync(async db => { db.DeliveryStatusLogs.Add(new() { OrderId = order.Id, FromStatus = "IN_TRANSIT", ToStatus = "ARRIVED", CreatedAt = DateTime.UtcNow.AddHours(-25) }); await db.SaveChangesAsync(); });
        await using var db = Context(new FailSave(PickupTimeoutService.AuditAction));
        var timeout = new PickupTimeoutService(db, Options.Create(new PickupTimeoutOptions()), new OrderTransitionWriter(db));
        await Assert.ThrowsAsync<DbUpdateException>(() => timeout.ExpireOverdueAsync(DateTime.UtcNow));
        await factory.WithDbAsync(async outside => {
            var saved = (await outside.DeliveryOrders.FindAsync(order.Id))!; Assert.Equal("ARRIVED", saved.Status); Assert.Null(saved.CancelReason);
            Assert.Equal(0, await outside.DeliveryFailureEpisodes.CountAsync(e => e.FailureLog.OrderId == order.Id));
            Assert.Equal(1, await outside.DeliveryStatusLogs.CountAsync(l => l.OrderId == order.Id));
            Assert.Equal(0, await outside.Notifications.CountAsync(n => n.OrderId == order.Id));
            Assert.Equal(0, await outside.AuditLogs.CountAsync(a => a.EntityId == order.Id.ToString()));
        });
    }

    [PostgresFact]
    public async Task Competing_manual_reports_record_exactly_one_episode_and_failure_effects()
    {
        using var client = factory.CreateClient();
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await FailureData.InAirAsync(factory.Services);
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => SchedulingData.Send(client, actor.Token,
            $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = "HARDWARE_FAULT", reason = "Motor failed" })));
        try { Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict)); }
        finally { foreach (var r in responses) r.Dispose(); }
        await factory.WithDbAsync(async db => {
            Assert.Equal(1, await db.DeliveryFailureEpisodes.CountAsync(e => e.FailureLog.OrderId == order.Id));
            Assert.Equal(1, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == order.Id));
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.OrderId == order.Id));
            Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityId == order.Id.ToString()));
        });
        var detail = await FailureData.DetailAsync(client, actor.Token, order.Id);
        Assert.Equal("HARDWARE_FAULT", detail.GetProperty("latestFailure").GetProperty("failureType").GetString());
        using var queue = await SchedulingData.Send(client, actor.Token, $"/api/dispatch/failed-orders?searchKeyword={order.TrackingNumber}", HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
    }

    [PostgresFact]
    public async Task Competing_resolution_paths_cannot_record_two_decisions_or_release_return_resources()
    {
        using var client = factory.CreateClient();
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await FailureData.InAirAsync(factory.Services);
        using var report = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = "MISSION_ABORTED", reason = "Stopped" });
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        var actions = new[] { "RESCHEDULE", "CANCEL", "RETURN_TO_ORIGIN" };
        var responses = await Task.WhenAll(actions.Select(action => SchedulingData.Send(client, actor.Token,
            $"/api/delivery-orders/{order.Id}/resolve-failure", HttpMethod.Post, new { action, note = "Checked package", packageVerified = true })));
        try { Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict)); }
        finally { foreach (var r in responses) r.Dispose(); }
        await factory.WithDbAsync(async db => {
            var handling = await db.FailureHandlingEvents.SingleAsync(e => e.FailureLog.OrderId == order.Id);
            var saved = (await db.DeliveryOrders.FindAsync(order.Id))!;
            Assert.Equal(handling.Action == "RETURN_TO_ORIGIN" ? "FAILED" : handling.Action == "CANCEL" ? "CANCELLED" : "APPROVED", saved.Status);
            var mission = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id);
            Assert.Equal(handling.Action == "RETURN_TO_ORIGIN" ? "RESERVED" : "EMPTY", (await db.StationSlots.FindAsync(mission.DestinationSlotId))!.Status);
            Assert.Equal("FLYING", (await db.Drones.FindAsync(mission.DroneId))!.Status);
            Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.EntityId == order.Id.ToString()));
        });
    }

    [PostgresFact]
    public async Task Failure_metadata_save_error_rolls_back_writer_status_mission_and_effects()
    {
        _ = factory.CreateClient();
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await FailureData.InAirAsync(factory.Services);
        await using var db = Context(new FailSave("DELIVERY_FAILURE_DETECTED"));
        var service = new FailureService(db, new OrderTransitionWriter(db), TimeProvider.System);
        await Assert.ThrowsAsync<DbUpdateException>(() => service.ReportAsync(order.Id, actor.Id, new("HARDWARE_FAULT", "Failed motor")));
        await factory.WithDbAsync(async saved => {
            Assert.Equal("IN_TRANSIT", (await saved.DeliveryOrders.FindAsync(order.Id))!.Status);
            Assert.Equal("IN_AIR", (await saved.FlightMissions.SingleAsync(m => m.OrderId == order.Id)).FlightStatus);
            Assert.Equal(0, await saved.DeliveryFailureEpisodes.CountAsync(e => e.FailureLog.OrderId == order.Id));
            Assert.Equal(0, await saved.DeliveryStatusLogs.CountAsync(l => l.OrderId == order.Id));
            Assert.Equal(0, await saved.Notifications.CountAsync(n => n.OrderId == order.Id));
            Assert.Equal(0, await saved.AuditLogs.CountAsync(a => a.EntityId == order.Id.ToString()));
        });
    }

    [PostgresFact]
    public async Task Resolution_save_error_rolls_back_event_transition_schedule_and_slot_release()
    {
        using var client = factory.CreateClient();
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await FailureData.InAirAsync(factory.Services);
        using var report = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = "MISSION_ABORTED", reason = "Stopped" });
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        await using var db = Context(new FailSave("FAILED_DELIVERY_RESOLVED"));
        var service = new FailureService(db, new OrderTransitionWriter(db), TimeProvider.System);
        await Assert.ThrowsAsync<DbUpdateException>(() => service.ResolveAsync(order.Id, actor.Id, new("RESCHEDULE", "Package verified", true)));
        await factory.WithDbAsync(async saved => {
            var o = (await saved.DeliveryOrders.FindAsync(order.Id))!;
            Assert.Equal("FAILED", o.Status); Assert.NotNull(o.ScheduledDepartureTime); Assert.NotNull(o.EstimatedDeliveryTime);
            var mission = await saved.FlightMissions.SingleAsync(m => m.OrderId == order.Id);
            Assert.Equal("RESERVED", (await saved.StationSlots.FindAsync(mission.DestinationSlotId))!.Status);
            Assert.Equal(0, await saved.FailureHandlingEvents.CountAsync(e => e.FailureLog.OrderId == order.Id));
            Assert.Equal(1, await saved.DeliveryStatusLogs.CountAsync(l => l.OrderId == order.Id));
            Assert.Equal(1, await saved.Notifications.CountAsync(n => n.OrderId == order.Id));
        });
    }

    [PostgresFact]
    public async Task Trusted_staging_respects_caller_transaction_and_can_roll_back_after_save()
    {
        _ = factory.CreateClient();
        var order = await FailureData.InAirAsync(factory.Services);
        await using var db = Context();
        await using (var tx = await db.Database.BeginTransactionAsync()) {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM delivery_orders WHERE \"Id\" = {order.Id} FOR UPDATE");
            var saved = (await db.DeliveryOrders.FindAsync(order.Id))!;
            var writer = new OrderTransitionWriter(db);
            await new FailureService(db, writer, TimeProvider.System).StageInAirFailureAsync(saved, "MISSION_ABORTED", "Automatic abort", null, "SIMULATOR_ABORT", DateTime.UtcNow);
            await db.SaveChangesAsync();
            await factory.WithDbAsync(async outside => Assert.Equal("IN_TRANSIT", (await outside.DeliveryOrders.FindAsync(order.Id))!.Status));
            await tx.RollbackAsync();
        }
        await factory.WithDbAsync(async saved => {
            Assert.Equal("IN_TRANSIT", (await saved.DeliveryOrders.FindAsync(order.Id))!.Status);
            Assert.Equal(0, await saved.DeliveryFailureEpisodes.CountAsync(e => e.FailureLog.OrderId == order.Id));
        });
    }

    [PostgresFact]
    public async Task Intake_rejection_metadata_failure_rolls_back_existing_planned_resource_cleanup()
    {
        using var client = factory.CreateClient();
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        var drone = await SchedulingData.ResourcesAsync(factory.Services, order);
        using var schedule = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/schedule", HttpMethod.Put, new { scheduledDepartureTime = DateTime.UtcNow.AddHours(1) });
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);
        using var assigned = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/flight-mission", HttpMethod.Post, new { droneId = drone.Id });
        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        await using var db = Context(new FailSave("PACKAGE_INTAKE_REJECT"));
        await Assert.ThrowsAsync<DbUpdateException>(() => new StationOperationService(db, new OrderTransitionWriter(db)).RejectIntakeAsync(actor.Id, order.OriginStationId, new() { TrackingNumber = order.TrackingNumber, Reason = "Damaged package" }));
        await factory.WithDbAsync(async saved => {
            Assert.Equal("APPROVED", (await saved.DeliveryOrders.FindAsync(order.Id))!.Status);
            var mission = await saved.FlightMissions.SingleAsync(m => m.OrderId == order.Id);
            Assert.Equal("PLANNED", mission.FlightStatus); Assert.Equal("ASSIGNED", (await saved.Drones.FindAsync(drone.Id))!.Status);
            Assert.Equal("RESERVED", (await saved.StationSlots.FindAsync(mission.DestinationSlotId))!.Status);
            Assert.Equal(0, await saved.DeliveryFailureEpisodes.CountAsync(e => e.FailureLog.OrderId == order.Id));
        });
    }

    private AppDbContext Context(IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(factory.DatabaseConnectionString);
        if (interceptor != null) options.AddInterceptors(interceptor);
        return new(options.Options);
    }
    private sealed class FailSave(string action) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.Entity.Action == action)) throw new DbUpdateException("Injected failure after persistence but before commit");
            return base.SavedChangesAsync(eventData, result, ct);
        }
    }

    private sealed class PauseOccupiedRead : DbCommandInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _paused;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("station_slots", StringComparison.Ordinal) && command.CommandText.Contains("'OCCUPIED'", StringComparison.Ordinal)
                && Interlocked.Exchange(ref _paused, 1) == 0) {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
            return result;
        }
    }

    private async Task<bool> WaitForOrderLockAsync(Task pending)
    {
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString); await connection.OpenAsync();
        for (var i = 0; i < 150 && !pending.IsCompleted; i++) {
            await using var query = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%delivery_orders%')", connection);
            if ((bool)(await query.ExecuteScalarAsync())!) return true;
            await Task.Delay(20);
        }
        return false;
    }
}
