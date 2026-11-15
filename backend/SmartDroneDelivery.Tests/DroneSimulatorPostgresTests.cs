using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class DroneSimulatorPostgresTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private readonly SimulatorClock _clock = new();

    [PostgresFact]
    public async Task Tick_joins_caller_transaction_without_committing_or_discarding_unrelated_tracking()
    {
        _ = factory.CreateClient();
        var id = await SeedAsync();
        await using var db = Context();
        var mission = await db.FlightMissions.Include(m => m.Order).ThenInclude(o => o.Customer).SingleAsync(m => m.Id == id);
        var customer = mission.Order.Customer;
        var originalName = customer.FullName;
        await using var tx = await db.Database.BeginTransactionAsync();
        customer.FullName = "Caller owned change";
        await Service(db).TickAsync();
        Assert.Same(tx, db.Database.CurrentTransaction);
        Assert.Same(customer, db.ChangeTracker.Entries<User>().Single(e => e.Entity.Id == customer.Id).Entity);
        Assert.Equal("Caller owned change", customer.FullName);
        await AssertStateAsync(id, "READY_FOR_TAKEOFF", "PLANNED", 0, 0);
        await tx.RollbackAsync();
        await AssertStateAsync(id, "READY_FOR_TAKEOFF", "PLANNED", 0, 0);
        await factory.WithDbAsync(async outside => Assert.Equal(originalName, (await outside.Users.FindAsync(customer.Id))!.FullName));
    }

    [PostgresFact]
    public async Task Actual_intake_claim_supersedes_a_later_created_historical_destination_claim()
    {
        _ = factory.CreateClient();
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        Guid id = default;
        await factory.WithDbAsync(async db => id = await SimulatorData.IntakeAfterHistoricalUseAsync(db, _clock.Now, actor.Id));
        await RaceTicksAsync();
        await AssertStateAsync(id, "IN_TRANSIT", "IN_AIR", 1, 2);
        await factory.WithDbAsync(async db => {
            var current = (await db.FlightMissions.FindAsync(id))!;
            Assert.Equal("EMPTY", (await db.StationSlots.FindAsync(current.StartSlotId))!.Status);
        });
    }

    [PostgresFact]
    public async Task Concurrent_workers_start_progress_and_arrive_once_at_persisted_microsecond_precision()
    {
        _ = factory.CreateClient();
        _clock.Now = _clock.Now.AddTicks(7);
        var id = await SeedAsync();
        await RaceTicksAsync();
        await AssertStateAsync(id, "IN_TRANSIT", "IN_AIR", 1, 1);
        // PostgreSQL truncates the seven extra 100ns ticks. The identical fixed
        // clock must compare at that precision, including on a new DbContext.
        await RaceTicksAsync();
        await AssertStateAsync(id, "IN_TRANSIT", "IN_AIR", 1, 1);
        _clock.Now = _clock.Now.AddSeconds(10);
        await RaceTicksAsync();
        await AssertStateAsync(id, "IN_TRANSIT", "IN_AIR", 2, 1);
        _clock.Now = _clock.Now.AddMinutes(2);
        await RaceTicksAsync();
        await AssertStateAsync(id, "ARRIVED", "COMPLETED", 3, 2);
        await factory.WithDbAsync(async db => {
            var mission = await db.FlightMissions.Include(m => m.Order).Include(m => m.Drone).SingleAsync(m => m.Id == id);
            Assert.Null(mission.Order.ActualDeliveryTime);
            Assert.Equal("CHARGING", mission.Drone.Status); Assert.Equal(mission.Order.DestinationStationId, mission.Drone.CurrentStationId);
            Assert.Equal("EMPTY", (await db.StationSlots.FindAsync(mission.StartSlotId))!.Status);
            Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(mission.DestinationSlotId))!.Status);
            Assert.Equal(.01m, (await db.FlightTelemetryLogs.Where(l => l.MissionId == id).OrderByDescending(l => l.Timestamp).FirstAsync()).Longitude);
        });
    }

    [PostgresFact]
    public async Task Post_save_failure_rolls_back_takeoff_and_arrival_including_the_sample_and_resources()
    {
        _ = factory.CreateClient();
        var id = await SeedAsync();
        await using (var failing = Context(new FailAfterSave()))
            await Assert.ThrowsAsync<DbUpdateException>(() => Service(failing).TickAsync());
        await AssertStateAsync(id, "READY_FOR_TAKEOFF", "PLANNED", 0, 0);
        await factory.WithDbAsync(async db => {
            var m = await db.FlightMissions.Include(m => m.Order).Include(m => m.Drone).SingleAsync(m => m.Id == id);
            Assert.Null(m.StartTime); Assert.Null(m.Order.ActualDepartureTime);
            Assert.Equal("ASSIGNED", m.Drone.Status); Assert.Equal(m.Order.OriginStationId, m.Drone.CurrentStationId);
            Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(m.StartSlotId))!.Status);
        });
        await TickAsync();
        _clock.Now = _clock.Now.AddMinutes(2);
        await using (var failing = Context(new FailAfterSave()))
            await Assert.ThrowsAsync<DbUpdateException>(() => Service(failing).TickAsync());
        await AssertStateAsync(id, "IN_TRANSIT", "IN_AIR", 1, 1);
        await factory.WithDbAsync(async db => {
            var m = await db.FlightMissions.Include(m => m.Drone).SingleAsync(m => m.Id == id);
            Assert.Null(m.EndTime); Assert.Equal("FLYING", m.Drone.Status); Assert.Null(m.Drone.CurrentStationId);
            Assert.Equal("RESERVED", (await db.StationSlots.FindAsync(m.DestinationSlotId))!.Status);
        });
        await TickAsync();
        await AssertStateAsync(id, "ARRIVED", "COMPLETED", 2, 2);
    }

    [PostgresFact]
    public async Task Waiting_tick_rechecks_committed_cancellation_and_cannot_resurrect_it()
    {
        _ = factory.CreateClient();
        var id = await SeedAsync();
        Guid orderId = default;
        await factory.WithDbAsync(async db => orderId = (await db.FlightMissions.FindAsync(id))!.OrderId);
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync(); await using var tx = await connection.BeginTransactionAsync();
        await using (var update = new NpgsqlCommand("UPDATE delivery_orders SET \"Status\" = 'CANCELLED' WHERE \"Id\" = @id", connection, tx)) {
            update.Parameters.AddWithValue("id", orderId); await update.ExecuteNonQueryAsync();
        }
        var app = $"la332-wait-{Guid.NewGuid():N}";
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(
            new NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString) { ApplicationName = app }.ConnectionString).Options);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = Service(db).TickAsync(timeout.Token);
        await using var observer = new NpgsqlConnection(factory.DatabaseConnectionString); await observer.OpenAsync();
        var waited = false;
        try {
            for (var i = 0; i < 100; i++) {
                await using var query = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE application_name = @app AND wait_event_type = 'Lock')", observer);
                query.Parameters.AddWithValue("app", app);
                if (await query.ExecuteScalarAsync() is true) { waited = true; break; }
                await Task.Delay(20);
            }
        }
        finally { await tx.CommitAsync(); }
        await pending;
        Assert.True(waited);
        await AssertStateAsync(id, "CANCELLED", "PLANNED", 0, 0);
    }

    [PostgresFact]
    public async Task Waiting_tick_rechecks_destination_resource_changes_before_departure()
    {
        _ = factory.CreateClient();
        var id = await SeedAsync(); Guid slotId = default;
        await factory.WithDbAsync(async db => slotId = (await db.FlightMissions.FindAsync(id))!.DestinationSlotId!.Value);
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString); await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        await using (var update = new NpgsqlCommand("UPDATE station_slots SET \"Status\" = 'ERROR' WHERE \"Id\" = @id", connection, tx)) {
            update.Parameters.AddWithValue("id", slotId); await update.ExecuteNonQueryAsync();
        }
        var pending = TickAsync();
        // A separate connection observes the actual row-lock wait, without timing guesses.
        await using var observer = new NpgsqlConnection(factory.DatabaseConnectionString); await observer.OpenAsync();
        var waited = false;
        try {
            for (var i = 0; i < 100; i++) {
                await using var query = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%station_slots%')", observer);
                if (await query.ExecuteScalarAsync() is true) { waited = true; break; }
                await Task.Delay(20);
            }
        }
        finally { await tx.CommitAsync(); }
        await pending;
        Assert.True(waited);
        await AssertStateAsync(id, "READY_FOR_TAKEOFF", "PLANNED", 0, 0);
    }

    private async Task<Guid> SeedAsync()
    {
        Guid id = default;
        await factory.WithDbAsync(async db => id = await SimulatorData.SeedAsync(db, _clock.Now.AddSeconds(-1)));
        return id;
    }
    private Task RaceTicksAsync() => Task.WhenAll(Enumerable.Range(0, 6).Select(_ => TickAsync()));
    private async Task TickAsync() { await using var db = Context(); await Service(db).TickAsync(); }
    private DroneSimulatorService Service(AppDbContext db) => new(db, new OrderTransitionWriter(db),
        new FlightTelemetryService(db, _clock), _clock, Options.Create(new DroneSimulatorOptions { Enabled = true }), Options.Create(new DeliveryFlightOptions()));
    private AppDbContext Context(IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(factory.DatabaseConnectionString);
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options);
    }
    private Task AssertStateAsync(Guid id, string orderStatus, string missionStatus, int samples, int changes) => factory.WithDbAsync(async db => {
        var m = await db.FlightMissions.Include(m => m.Order).SingleAsync(m => m.Id == id);
        Assert.Equal(orderStatus, m.Order.Status); Assert.Equal(missionStatus, m.FlightStatus);
        Assert.Equal(samples, await db.FlightTelemetryLogs.CountAsync(l => l.MissionId == id));
        Assert.Equal(changes, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == m.OrderId));
        Assert.Equal(changes, await db.Notifications.CountAsync(n => n.OrderId == m.OrderId));
    });
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default) =>
            throw new DbUpdateException("Injected failure after persistence, before commit");
    }
}
