using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class DroneSimulatorAcceptancePostgresTests
{
    [PostgresFact]
    public async Task Http_samples_roundtrip_storage_boundaries_and_submicrosecond_ties_with_late_delivery()
    {
        using var factory = new PostgresApiFactory();
        using var client = factory.CreateClient();
        var admin = await ReviewData.UserAsync(factory.Services, "ADMIN");
        Guid id = default;
        await factory.WithDbAsync(async db => id = await TelemetryTestData.SeedMissionAsync(db));
        TelemetrySampleRequest[] samples = [
            new() { Latitude = -90, Longitude = -180, AltitudeMeters = 0, SpeedMps = 0, BatteryPercentage = 0, Timestamp = "2020-01-01T00:00:00.1234561Z" },
            new() { Latitude = 90, Longitude = 180, AltitudeMeters = 9999.99m, SpeedMps = 999.99m, BatteryPercentage = 100, Timestamp = "2020-01-01T00:00:00.1234569+00:00" },
            new() { Latitude = 0, Longitude = 0, AltitudeMeters = 0, SpeedMps = 0, BatteryPercentage = 50, Timestamp = "2020-01-01T00:00:00.1234559Z" }
        ];
        foreach (var sample in samples) {
            using var response = await DroneSimulatorAcceptanceTests.Post(client, admin.Token, id, sample);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
        // Read with a new context: the two distinct wire timestamps now tie at database precision.
        await factory.WithDbAsync(async db => {
            var rows = await db.FlightTelemetryLogs.Where(l => l.MissionId == id).OrderBy(l => l.Id).ToListAsync();
            Assert.Equal(3, rows.Count);
            Assert.Equal(rows[0].Timestamp, rows[1].Timestamp);
            Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(1234560), rows[1].Timestamp);
            Assert.Equal(-90m, rows[0].Latitude); Assert.Equal(-180m, rows[0].Longitude);
            var latest = await new FlightTelemetryService(db, TimeProvider.System).GetLatestAsync(id);
            Assert.Equal(TelemetryAvailability.Stale, latest.Availability);
            Assert.Equal(90m, latest.LastPosition!.Latitude); Assert.Equal(180m, latest.LastPosition.Longitude);
            Assert.Equal(9999.99m, latest.LastPosition.AltitudeMeters); Assert.Equal(999.99m, latest.LastPosition.SpeedMps);
            Assert.Equal(100, latest.LastPosition.BatteryPercentage);
            Assert.Equal(rows[1].Timestamp, latest.LastTelemetryAt);
            Assert.Equal(DateTimeKind.Utc, latest.LastPosition.Timestamp.Kind);
            var orderId = await db.FlightMissions.Where(m => m.Id == id).Select(m => m.OrderId).SingleAsync();
            Assert.Empty(await db.DeliveryStatusLogs.Where(l => l.OrderId == orderId).ToListAsync());
            Assert.Empty(await db.Notifications.Where(n => n.OrderId == orderId).ToListAsync());
        });
    }

    [PostgresFact]
    public async Task Ingestion_waiting_behind_actual_simulator_abort_observes_commit_or_rollback()
    {
        foreach (var commit in new[] { false, true }) {
            using var factory = new PostgresApiFactory();
            using var client = factory.CreateClient();
            var clock = new SimulatorClock();
            Guid id = default;
            await factory.WithDbAsync(async db => id = await SimulatorData.SeedAsync(db, clock.Now));
            await factory.WithDbAsync(db => Simulator(db, clock).TickAsync());
            clock.Now = clock.Now.AddSeconds(20);
            await using var abortDb = Context(factory);
            await using var transaction = await abortDb.Database.BeginTransactionAsync();
            await Simulator(abortDb, clock).TickAsync();

            var application = $"la334-ingestion-{Guid.NewGuid():N}";
            await using var ingestionDb = Context(factory, application);
            // A request context may have seen IN_AIR before the abort took its mission lock.
            Assert.Equal("IN_AIR", (await ingestionDb.FlightMissions.FindAsync(id))!.FlightStatus);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var ingestion = new FlightTelemetryService(ingestionDb, clock).IngestAsync(id,
                DroneSimulatorAcceptanceTests.Sample(clock.Now, .002m), timeout.Token);
            bool blocked;
            try { blocked = await ObserveLock(factory, application); }
            finally {
                if (commit) await transaction.CommitAsync();
                else await transaction.RollbackAsync();
            }
            if (commit) {
                var error = await Assert.ThrowsAsync<ApiException>(() => ingestion);
                Assert.Equal(409, error.StatusCode);
            }
            else await ingestion;
            Assert.True(blocked, "Ingestion must wait for the simulator's uncommitted mission update.");
            await factory.WithDbAsync(async db => {
                var mission = await db.FlightMissions.Include(m => m.Order).SingleAsync(m => m.Id == id);
                Assert.Equal(commit ? "FAILED" : "IN_TRANSIT", mission.Order.Status);
                Assert.Equal(commit ? "ABORTED" : "IN_AIR", mission.FlightStatus);
                Assert.Equal(commit ? 1 : 2, await db.FlightTelemetryLogs.CountAsync(l => l.MissionId == id));
                Assert.Equal(commit ? 2 : 1, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == mission.OrderId));
                Assert.Equal(commit ? 2 : 1, await db.Notifications.CountAsync(n => n.OrderId == mission.OrderId));
                Assert.Equal(commit ? 1 : 0, await db.DeliveryFailureEpisodes.CountAsync(e => e.FailureLog.OrderId == mission.OrderId));
                Assert.Equal(commit, await db.SimulatorDiversions.AnyAsync(d => d.MissionId == id));
                var latest = await new FlightTelemetryService(db, clock).GetLatestAsync(id);
                Assert.Equal(commit ? TelemetryAvailability.NotInAir : TelemetryAvailability.Fresh, latest.Availability);
                if (commit) Assert.Null(latest.LastPosition);
                else Assert.Equal(.002m, latest.LastPosition!.Longitude);
            });
        }
    }

    private static DroneSimulatorService Simulator(AppDbContext db, SimulatorClock clock) => new(db, new OrderTransitionWriter(db),
        new FlightTelemetryService(db, clock), clock, Options.Create(new DroneSimulatorOptions { Enabled = true, AbortAfterSeconds = 20 }), Options.Create(new DeliveryFlightOptions()));

    private static AppDbContext Context(PostgresApiFactory factory, string? application = null) {
        var connection = new NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString);
        if (application is not null) connection.ApplicationName = application;
        return new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options);
    }

    private static async Task<bool> ObserveLock(PostgresApiFactory factory, string application) {
        await using var observer = new NpgsqlConnection(factory.DatabaseConnectionString);
        await observer.OpenAsync();
        for (var attempt = 0; attempt < 150; attempt++) {
            await using var query = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE application_name = @app AND wait_event_type = 'Lock')", observer);
            query.Parameters.AddWithValue("app", application);
            if (await query.ExecuteScalarAsync() is true) return true;
            await Task.Delay(20);
        }
        return false;
    }
}
