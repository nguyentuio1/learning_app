using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class FlightTelemetryReadTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 10, 0, TimeSpan.Zero);

    [Fact]
    public async Task No_sample_is_explicitly_unavailable_without_an_invented_position()
    {
        await factory.WithDbAsync(async db =>
        {
            var id = await TelemetryTestData.SeedMissionAsync(db);
            var snapshot = await new FlightTelemetryService(db, new TelemetryTestClock(Now)).GetLatestAsync(id);
            Assert.Equal(TelemetryAvailability.NoSample, snapshot.Availability);
            Assert.True(snapshot.TelemetryStale);
            Assert.Null(snapshot.LastPosition);
            Assert.Null(snapshot.LastTelemetryAt);
        });
    }

    [Theory]
    [InlineData(60, false)]
    [InlineData(61, true)]
    public async Task Staleness_is_strictly_more_than_sixty_seconds(int age, bool stale)
    {
        await factory.WithDbAsync(async db =>
        {
            var id = await TelemetryTestData.SeedMissionAsync(db);
            var service = new FlightTelemetryService(db, new TelemetryTestClock(Now));
            await service.IngestAsync(id, Sample(Now.AddSeconds(-age)));
            var snapshot = await service.GetLatestAsync(id);
            Assert.Equal(stale, snapshot.TelemetryStale);
            Assert.Equal(stale ? TelemetryAvailability.Stale : TelemetryAvailability.Fresh, snapshot.Availability);
            Assert.NotNull(snapshot.LastPosition);
            Assert.Equal(Now.AddSeconds(-age).UtcDateTime, snapshot.LastTelemetryAt);
            Assert.Equal(DateTimeKind.Utc, snapshot.LastPosition!.Timestamp.Kind);
        });
    }

    [Fact]
    public async Task A_sample_becomes_stale_as_time_passes_without_mutating_the_order()
    {
        await factory.WithDbAsync(async db =>
        {
            var id = await TelemetryTestData.SeedMissionAsync(db);
            var clock = new TelemetryTestClock(Now);
            var service = new FlightTelemetryService(db, clock);
            await service.IngestAsync(id, Sample(Now));
            Assert.False((await service.GetLatestAsync(id)).TelemetryStale);
            clock.Now = Now.AddSeconds(61);
            Assert.True((await service.GetLatestAsync(id)).TelemetryStale);
            Assert.Equal("IN_AIR", (await db.FlightMissions.FindAsync(id))!.FlightStatus);
        });
    }

    [Fact]
    public async Task Late_samples_are_retained_but_cannot_move_the_latest_position_backwards()
    {
        await factory.WithDbAsync(async db =>
        {
            var id = await TelemetryTestData.SeedMissionAsync(db);
            var service = new FlightTelemetryService(db, new TelemetryTestClock(Now));
            await service.IngestAsync(id, Sample(Now.AddSeconds(-5), 10));
            await service.IngestAsync(id, Sample(Now.AddSeconds(-40), 20));
            var snapshot = await service.GetLatestAsync(id);
            Assert.Equal(10, snapshot.LastPosition!.Latitude);
            Assert.Equal(Now.AddSeconds(-5).UtcDateTime, snapshot.LastTelemetryAt);
            Assert.Equal(2, await db.FlightTelemetryLogs.CountAsync(x => x.MissionId == id));
        });
    }

    [Fact]
    public async Task Equal_timestamps_have_a_deterministic_latest_row()
    {
        await factory.WithDbAsync(async db =>
        {
            var id = await TelemetryTestData.SeedMissionAsync(db);
            var service = new FlightTelemetryService(db, new TelemetryTestClock(Now));
            await service.IngestAsync(id, Sample(Now, 10));
            await service.IngestAsync(id, Sample(Now, 20));
            Assert.Equal(20, (await service.GetLatestAsync(id)).LastPosition!.Latitude);
        });
    }

    [Theory]
    [InlineData("PLANNED")]
    [InlineData("COMPLETED")]
    [InlineData("ABORTED")]
    public async Task Historical_samples_are_not_exposed_as_live_positions_for_non_airborne_missions(string status)
    {
        await factory.WithDbAsync(async db =>
        {
            var id = await TelemetryTestData.SeedMissionAsync(db, status);
            db.FlightTelemetryLogs.Add(new FlightTelemetryLog
            {
                MissionId = id, Latitude = 10, Longitude = 106, Timestamp = Now.UtcDateTime
            });
            await db.SaveChangesAsync();
            var snapshot = await new FlightTelemetryService(db, new TelemetryTestClock(Now)).GetLatestAsync(id);
            Assert.Equal(TelemetryAvailability.NotInAir, snapshot.Availability);
            Assert.Null(snapshot.LastPosition);
            Assert.Equal(Now.UtcDateTime, snapshot.LastTelemetryAt);
        });
    }

    [Fact]
    public async Task Unknown_mission_is_not_misreported_as_no_telemetry()
    {
        await factory.WithDbAsync(async db =>
        {
            var error = await Assert.ThrowsAsync<ApiException>(() =>
                new FlightTelemetryService(db, new TelemetryTestClock(Now)).GetLatestAsync(Guid.NewGuid()));
            Assert.Equal(404, error.StatusCode);
        });
    }

    internal static TelemetrySampleRequest Sample(DateTimeOffset timestamp, decimal latitude = 10.7769m) => new()
    {
        Latitude = latitude, Longitude = 106.7009m, AltitudeMeters = 80,
        SpeedMps = 11.5m, BatteryPercentage = 71, Timestamp = timestamp.ToString("O")
    };
}

internal class TelemetryTestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
