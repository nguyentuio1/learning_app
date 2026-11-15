using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class FlightTelemetryPostgresTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [PostgresFact]
    public async Task Append_and_latest_projection_work_with_real_storage_precision()
    {
        await factory.WithDbAsync(async db =>
        {
            var id = await TelemetryTestData.SeedMissionAsync(db);
            var now = new DateTimeOffset(2026, 11, 12, 8, 10, 0, TimeSpan.Zero);
            var service = new FlightTelemetryService(db, new TelemetryTestClock(now));
            var sample = FlightTelemetryReadTests.Sample(now.AddSeconds(-5));
            sample.AltitudeMeters = 9999.99m; sample.SpeedMps = 999.99m;
            await service.IngestAsync(id, sample);
            await service.IngestAsync(id, FlightTelemetryReadTests.Sample(now.AddSeconds(-30), 20));
            var latest = await service.GetLatestAsync(id);
            Assert.Equal(10.7769m, latest.LastPosition!.Latitude);
            Assert.Equal(9999.99m, latest.LastPosition.AltitudeMeters);
            Assert.Equal(999.99m, latest.LastPosition.SpeedMps);
            Assert.Equal(now.AddSeconds(-5).UtcDateTime, latest.LastTelemetryAt);
            Assert.Equal(2, await db.FlightTelemetryLogs.CountAsync(x => x.MissionId == id));
        });
    }

    [PostgresFact]
    public async Task Ingestion_joins_the_callers_transaction_without_committing_it()
    {
        Guid id = default;
        await factory.WithDbAsync(async db => id = await TelemetryTestData.SeedMissionAsync(db));
        await factory.WithDbAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var service = new FlightTelemetryService(db, TimeProvider.System);
            await service.IngestAsync(id, FlightTelemetryReadTests.Sample(DateTimeOffset.UtcNow.AddSeconds(-1)));
            Assert.Same(tx, db.Database.CurrentTransaction);
            await tx.RollbackAsync();
        });
        await factory.WithDbAsync(async db => Assert.Equal(0, await db.FlightTelemetryLogs.CountAsync(x => x.MissionId == id)));
    }

    [PostgresFact]
    public async Task Ingestion_waits_for_a_concurrent_landing_and_then_refuses_the_completed_mission()
    {
        Guid id = default;
        await factory.WithDbAsync(async db => id = await TelemetryTestData.SeedMissionAsync(db));
        await using var landing = new NpgsqlConnection(factory.DatabaseConnectionString);
        await landing.OpenAsync();
        await using var transaction = await landing.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("UPDATE flight_missions SET \"FlightStatus\" = 'COMPLETED' WHERE \"Id\" = @id", landing, transaction))
        {
            command.Parameters.AddWithValue("id", id);
            await command.ExecuteNonQueryAsync();
        }

        var application = $"la331-race-{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString) { ApplicationName = application }.ConnectionString;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ingestion = new FlightTelemetryService(db, TimeProvider.System).IngestAsync(id,
            FlightTelemetryReadTests.Sample(DateTimeOffset.UtcNow.AddSeconds(-1)), timeout.Token);

        await using var observer = new NpgsqlConnection(factory.DatabaseConnectionString);
        await observer.OpenAsync();
        var observedLockWait = false;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var query = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE application_name = @app AND wait_event_type = 'Lock')", observer);
            query.Parameters.AddWithValue("app", application);
            if (await query.ExecuteScalarAsync() is true) { observedLockWait = true; break; }
            await Task.Delay(50);
        }
        await transaction.CommitAsync();
        var error = await Assert.ThrowsAsync<ApiException>(() => ingestion);
        Assert.Equal(409, error.StatusCode);
        Assert.True(observedLockWait, "Ingestion must wait for the mission row lock held by the landing transaction.");
        Assert.Equal(0, await db.FlightTelemetryLogs.CountAsync(x => x.MissionId == id));
    }
}
