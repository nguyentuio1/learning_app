using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class DroneSimulatorScenarioPostgresTests
{
    [PostgresFact]
    public async Task Concurrent_abort_and_landing_workers_persist_one_episode_and_one_landing()
    {
        using var factory = new PostgresApiFactory(); _ = factory.CreateClient();
        var clock = new SimulatorClock(); var id = await Seed(factory, clock);
        await Tick(factory, clock); clock.Now = clock.Now.AddSeconds(20).AddTicks(7);
        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Tick(factory, clock)));
        DateTime due = default;
        await factory.WithDbAsync(async db => {
            var m = await db.FlightMissions.Include(m => m.Order).Include(m => m.Drone).SingleAsync(m => m.Id == id);
            Assert.Equal("FAILED", m.Order.Status); Assert.Equal("ABORTED", m.FlightStatus);
            Assert.Equal("FLYING", m.Drone.Status); Assert.Null(m.Drone.CurrentStationId);
            Assert.Single(await db.DeliveryFailureEpisodes.ToListAsync());
            Assert.Single(await db.AuditLogs.Where(a => a.Action == "DELIVERY_FAILURE_DETECTED").ToListAsync());
            Assert.Equal(2, await db.Notifications.CountAsync(n => n.OrderId == m.OrderId));
            var d = Assert.Single(await db.SimulatorDiversions.ToListAsync()); due = d.LandingDueAt!.Value;
            Assert.Null(d.LandedAt); Assert.Equal(0, d.AbortedAt.Ticks % 10);
        });
        clock.Now = new DateTimeOffset(due.AddTicks(-10)); await Tick(factory, clock);
        await factory.WithDbAsync(async db => Assert.Null((await db.SimulatorDiversions.FindAsync(id))!.LandedAt));
        clock.Now = new DateTimeOffset(due.AddTicks(7));
        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Tick(factory, clock)));
        await factory.WithDbAsync(async db => {
            var d = await db.SimulatorDiversions.FindAsync(id); Assert.Equal(due, d!.LandedAt);
            var m = await db.FlightMissions.Include(m => m.Drone).SingleAsync(m => m.Id == id);
            Assert.Equal("CHARGING", m.Drone.Status); Assert.Equal(d.LandingStationId, m.Drone.CurrentStationId);
            Assert.Equal("RESERVED", (await db.StationSlots.FindAsync(m.DestinationSlotId))!.Status);
            Assert.Equal(2, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == m.OrderId));
            Assert.Single(await db.FlightTelemetryLogs.Where(l => l.MissionId == id).ToListAsync());
        });
    }

    [PostgresFact]
    public async Task Post_save_failure_rolls_back_abort_metadata_and_landing_physical_facts()
    {
        using var factory = new PostgresApiFactory(); _ = factory.CreateClient();
        var clock = new SimulatorClock(); var id = await Seed(factory, clock);
        await Tick(factory, clock); clock.Now = clock.Now.AddSeconds(20);
        await Assert.ThrowsAsync<DbUpdateException>(() => Tick(factory, clock, new FailAfterSave()));
        await factory.WithDbAsync(async db => {
            var m = await db.FlightMissions.Include(m => m.Order).SingleAsync(m => m.Id == id);
            Assert.Equal("IN_AIR", m.FlightStatus); Assert.Equal("IN_TRANSIT", m.Order.Status); Assert.Null(m.EndTime);
            Assert.Empty(await db.SimulatorDiversions.ToListAsync()); Assert.Empty(await db.DeliveryFailureEpisodes.ToListAsync());
            Assert.Single(await db.Notifications.Where(n => n.OrderId == m.OrderId).ToListAsync());
            Assert.Single(await db.DeliveryStatusLogs.Where(l => l.OrderId == m.OrderId).ToListAsync());
            Assert.Empty(await db.AuditLogs.Where(a => a.Action == "DELIVERY_FAILURE_DETECTED").ToListAsync());
        });
        await Tick(factory, clock); clock.Now = clock.Now.AddMinutes(1);
        await Assert.ThrowsAsync<DbUpdateException>(() => Tick(factory, clock, new FailAfterSave()));
        await factory.WithDbAsync(async db => {
            Assert.Null((await db.SimulatorDiversions.FindAsync(id))!.LandedAt);
            var m = await db.FlightMissions.Include(m => m.Drone).SingleAsync(m => m.Id == id);
            Assert.Equal("FLYING", m.Drone.Status); Assert.Null(m.Drone.CurrentStationId);
        });
        await Tick(factory, clock);
        await factory.WithDbAsync(async db => Assert.NotNull((await db.SimulatorDiversions.FindAsync(id))!.LandedAt));
    }

    [PostgresFact]
    public async Task Abort_and_recovery_join_caller_transaction_and_preserve_unrelated_tracking()
    {
        using var factory = new PostgresApiFactory(); _ = factory.CreateClient();
        var clock = new SimulatorClock(); var id = await Seed(factory, clock);
        await Tick(factory, clock); clock.Now = clock.Now.AddSeconds(20);
        foreach (var recovery in new[] { false, true }) {
            await using var db = Context(factory); await using var tx = await db.Database.BeginTransactionAsync();
            var mission = await db.FlightMissions.Include(m => m.Order).ThenInclude(o => o.Customer).SingleAsync(m => m.Id == id);
            var customer = mission.Order.Customer; var original = customer.FullName;
            customer.FullName = "Caller pending change";
            await Service(db, clock).TickAsync();
            Assert.Same(tx, db.Database.CurrentTransaction);
            Assert.Same(customer, db.ChangeTracker.Entries<User>().Single(e => e.Entity.Id == customer.Id).Entity);
            await factory.WithDbAsync(async outside => {
                Assert.Equal(original, (await outside.Users.FindAsync(customer.Id))!.FullName);
                if (recovery) Assert.Null((await outside.SimulatorDiversions.FindAsync(id))!.LandedAt);
                else Assert.Empty(await outside.SimulatorDiversions.ToListAsync());
            });
            await tx.RollbackAsync();
            if (!recovery) { await Tick(factory, clock); clock.Now = clock.Now.AddMinutes(1); }
        }
    }

    [PostgresFact]
    public async Task Tick_waiting_for_manual_failure_rechecks_status_and_does_not_duplicate_or_invent_diversion()
    {
        using var factory = new PostgresApiFactory(); _ = factory.CreateClient();
        var clock = new SimulatorClock(); var id = await Seed(factory, clock);
        await Tick(factory, clock); clock.Now = clock.Now.AddSeconds(20);
        await using var owner = Context(factory); await using var tx = await owner.Database.BeginTransactionAsync();
        var orderId = await owner.FlightMissions.Where(m => m.Id == id).Select(m => m.OrderId).SingleAsync();
        var order = (await owner.DeliveryOrders.FromSqlInterpolated($"SELECT * FROM delivery_orders WHERE \"Id\" = {orderId} FOR UPDATE").ToListAsync()).Single();
        await new FailureService(owner, new OrderTransitionWriter(owner), clock).StageInAirFailureAsync(order,
            "HARDWARE_FAULT", "Manual failure wins the race", null, "DISPATCH_REPORT", clock.Now.UtcDateTime);
        await owner.SaveChangesAsync();
        var app = $"la333-manual-{Guid.NewGuid():N}";
        await using var waiting = Context(factory, application: app);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = Service(waiting, clock).TickAsync(cancellation.Token);
        bool blocked;
        try { blocked = await ObserveLock(factory, app); }
        finally { await tx.CommitAsync(); }
        await pending; Assert.True(blocked);
        await factory.WithDbAsync(async db => {
            Assert.Empty(await db.SimulatorDiversions.ToListAsync());
            Assert.Equal("HARDWARE_FAULT", Assert.Single(await db.DeliveryFailureEpisodes.ToListAsync()).FailureType);
            Assert.Equal(2, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == orderId));
            var m = await db.FlightMissions.Include(m => m.Drone).SingleAsync(m => m.Id == id);
            Assert.Equal("FLYING", m.Drone.Status); Assert.Null(m.Drone.CurrentStationId);
        });
    }

    [PostgresFact]
    public async Task Landing_waits_for_drone_and_rechecks_new_active_mission_ownership()
    {
        using var factory = new PostgresApiFactory(); _ = factory.CreateClient();
        var clock = new SimulatorClock(); var id = await Seed(factory, clock);
        await Tick(factory, clock); clock.Now = clock.Now.AddSeconds(20); await Tick(factory, clock);
        clock.Now = clock.Now.AddMinutes(1);
        await using var owner = Context(factory);
        var otherId = await SimulatorData.SeedAsync(owner, clock.Now.AddDays(1));
        await using var tx = await owner.Database.BeginTransactionAsync();
        var prior = await owner.FlightMissions.FindAsync(id);
        await owner.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM drones WHERE \"Id\" = {prior!.DroneId} FOR UPDATE");
        var other = await owner.FlightMissions.FindAsync(otherId); other!.DroneId = prior.DroneId; await owner.SaveChangesAsync();
        var app = $"la333-owner-{Guid.NewGuid():N}";
        await using var waiting = Context(factory, application: app);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = Service(waiting, clock).TickAsync(cancellation.Token);
        bool blocked;
        try { blocked = await ObserveLock(factory, app); }
        finally { await tx.CommitAsync(); }
        await pending; Assert.True(blocked);
        await factory.WithDbAsync(async db => {
            Assert.Null((await db.SimulatorDiversions.FindAsync(id))!.LandedAt);
            var m = await db.FlightMissions.Include(m => m.Drone).SingleAsync(m => m.Id == id);
            Assert.Equal("FLYING", m.Drone.Status); Assert.Null(m.Drone.CurrentStationId);
        });
    }

    private static async Task<bool> ObserveLock(PostgresApiFactory factory, string application)
    {
        await using var observer = new NpgsqlConnection(factory.DatabaseConnectionString); await observer.OpenAsync();
        for (var i = 0; i < 150; i++) {
            await using var query = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE application_name = @app AND wait_event_type = 'Lock')", observer);
            query.Parameters.AddWithValue("app", application);
            if (await query.ExecuteScalarAsync() is true) return true;
            await Task.Delay(20);
        }
        return false;
    }
    private static AppDbContext Context(PostgresApiFactory factory, IInterceptor? interceptor = null, string? application = null)
    {
        var connection = new NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString);
        if (application is not null) connection.ApplicationName = application;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString);
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options);
    }
    private static DroneSimulatorService Service(AppDbContext db, SimulatorClock clock) => new(db, new OrderTransitionWriter(db),
        new FlightTelemetryService(db, clock), clock, Options.Create(new DroneSimulatorOptions { Enabled = true, AbortAfterSeconds = 20 }), Options.Create(new DeliveryFlightOptions()));
    private static async Task Tick(PostgresApiFactory factory, SimulatorClock clock, IInterceptor? interceptor = null)
    {
        await using var db = Context(factory, interceptor); await Service(db, clock).TickAsync();
    }
    private static async Task<Guid> Seed(PostgresApiFactory factory, SimulatorClock clock)
    {
        Guid id = default; await factory.WithDbAsync(async db => id = await SimulatorData.SeedAsync(db, clock.Now)); return id;
    }
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default) =>
            throw new DbUpdateException("Injected scenario failure after save, before commit");
    }
}
