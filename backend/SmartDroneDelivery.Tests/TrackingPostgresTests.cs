using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;
using SmartDroneDelivery.Api.Tracking;

namespace SmartDroneDelivery.Tests;

public class TrackingPostgresTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [PostgresFact]
    public async Task Snapshot_projection_selects_latest_mission_and_sample_on_PostgreSQL()
    {
        await factory.WithDbAsync(async db => {
            var id = await TelemetryTestData.SeedMissionAsync(db);
            var mission = await db.FlightMissions.Include(m => m.Order).SingleAsync(m => m.Id == id);
            db.FlightTelemetryLogs.AddRange(
                new FlightTelemetryLog { MissionId = id, Latitude = 11, Longitude = 106, Timestamp = DateTime.UtcNow.AddSeconds(-1) },
                new FlightTelemetryLog { MissionId = id, Latitude = 9, Longitude = 106, Timestamp = DateTime.UtcNow.AddMinutes(-2) });
            await db.SaveChangesAsync();
            var read = await new TrackingService(db, TimeProvider.System).GetAsync(mission.Order.CustomerId, mission.OrderId);
            Assert.Equal(id, read.MissionId);
            Assert.Equal(11m, read.Snapshot.LastPosition!.Latitude);
            Assert.False(read.Snapshot.TelemetryStale);
        });
    }

    [PostgresFact]
    public async Task Separate_tracking_reader_never_observes_uncommitted_status_or_telemetry()
    {
        Guid missionId = default, orderId = default, owner = default;
        await factory.WithDbAsync(async db => {
            missionId = await TelemetryTestData.SeedMissionAsync(db);
            var mission = await db.FlightMissions.Include(m => m.Order).SingleAsync(m => m.Id == missionId);
            orderId = mission.OrderId;
            owner = mission.Order.CustomerId;
        });
        var registry = new TrackingSubscriptions();
        var transport = new RecordingTrackingHub();
        var connection = new TrackingConnection("pg-viewer", owner, DateTimeOffset.UtcNow.AddMinutes(5), () => { });
        connection.Select(orderId);
        registry.Add(connection);
        var publisher = new TrackingBroadcaster(factory.Services.GetRequiredService<IServiceScopeFactory>(),
            registry, transport, TimeProvider.System, NullLogger<TrackingBroadcaster>.Instance);
        await publisher.PublishAsync();
        Assert.Single(transport.Events);
        Assert.Equal("IN_TRANSIT", Assert.IsType<SmartDroneDelivery.Api.DTOs.OrderDecisionDto>(transport.Events[0].Args[0]).Status);
        await factory.WithDbAsync(async writer => {
            await using var transaction = await writer.Database.BeginTransactionAsync();
            (await writer.DeliveryOrders.FindAsync(orderId))!.Status = "FAILED";
            writer.FlightTelemetryLogs.Add(new FlightTelemetryLog { MissionId = missionId, Latitude = 11,
                Longitude = 106, Timestamp = DateTime.UtcNow.AddSeconds(-1) });
            await writer.SaveChangesAsync();
            await using var reader = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(factory.DatabaseConnectionString).Options);
            var snapshot = (await new TrackingService(reader, TimeProvider.System).GetAsync(owner, orderId)).Snapshot;
            Assert.Equal("IN_TRANSIT", snapshot.Status);
            Assert.Null(snapshot.LastPosition);
            await publisher.PublishAsync();
            Assert.Single(transport.Events);
            await transaction.RollbackAsync();
        });
        await publisher.PublishAsync();
        Assert.Single(transport.Events);
    }
}
