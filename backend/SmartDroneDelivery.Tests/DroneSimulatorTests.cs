using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartDroneDelivery.Api.Services;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Tests;

public class DroneSimulatorTests(SimulatorApiFactory factory) : IClassFixture<SimulatorApiFactory>
{
    [Fact]
    public async Task Not_due_then_takeoff_progress_arrival_and_repeated_ticks_preserve_exact_effects()
    {
        _ = factory.CreateClient();
        var start = factory.Clock.GetUtcNow();
        Guid id = default;
        await factory.WithDbAsync(async db => id = await SimulatorData.SeedAsync(db, start.AddSeconds(10)));
        await SimulatorData.TickAsync(factory.Services);
        await CheckAsync(id, "READY_FOR_TAKEOFF", "PLANNED", 0, 0);
        factory.Clock.Now = start.AddSeconds(10);
        await SimulatorData.TickAsync(factory.Services);
        await SimulatorData.TickAsync(factory.Services);
        await CheckAsync(id, "IN_TRANSIT", "IN_AIR", 1, 1);
        await factory.WithDbAsync(async db => {
            var m = await db.FlightMissions.Include(x => x.Order).Include(x => x.Drone).SingleAsync(x => x.Id == id);
            Assert.Equal(factory.Clock.Now.UtcDateTime, m.StartTime);
            Assert.Equal(m.StartTime, m.Order.ActualDepartureTime);
            Assert.Equal("FLYING", m.Drone.Status); Assert.Null(m.Drone.CurrentStationId);
            Assert.Equal("EMPTY", (await db.StationSlots.FindAsync(m.StartSlotId))!.Status);
            Assert.Equal("RESERVED", (await db.StationSlots.FindAsync(m.DestinationSlotId))!.Status);
        });
        factory.Clock.Now = start.AddSeconds(20);
        await SimulatorData.TickAsync(factory.Services);
        await factory.WithDbAsync(async db => {
            var samples = await db.FlightTelemetryLogs.Where(x => x.MissionId == id).OrderBy(x => x.Timestamp).ToListAsync();
            Assert.Equal(2, samples.Count);
            Assert.InRange(samples[1].Longitude, .001m, .0012m);
            Assert.Equal(12m, samples[1].SpeedMps); Assert.Equal(80m, samples[1].AltitudeMeters);
        });
        factory.Clock.Now = start.AddSeconds(15); // clock regression cannot append or move backwards
        await SimulatorData.TickAsync(factory.Services);
        await CheckAsync(id, "IN_TRANSIT", "IN_AIR", 2, 1);
        factory.Clock.Now = start.AddMinutes(2);
        await SimulatorData.TickAsync(factory.Services);
        await SimulatorData.TickAsync(factory.Services);
        await CheckAsync(id, "ARRIVED", "COMPLETED", 3, 2);
        await factory.WithDbAsync(async db => {
            var m = await db.FlightMissions.Include(x => x.Order).Include(x => x.Drone).SingleAsync(x => x.Id == id);
            Assert.Null(m.Order.ActualDeliveryTime); Assert.Equal(factory.Clock.Now.UtcDateTime, m.EndTime);
            Assert.Equal("CHARGING", m.Drone.Status); Assert.Equal(m.Order.DestinationStationId, m.Drone.CurrentStationId);
            Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(m.DestinationSlotId))!.Status);
            var final = await db.FlightTelemetryLogs.Where(x => x.MissionId == id).OrderByDescending(x => x.Timestamp).FirstAsync();
            Assert.Equal(.01m, final.Longitude); Assert.Equal(0m, final.SpeedMps); Assert.Equal(0m, final.AltitudeMeters);
            Assert.All(await db.DeliveryStatusLogs.Where(x => x.OrderId == m.OrderId).ToListAsync(), x => Assert.Null(x.ChangedBy));
        });
    }

    [Theory]
    [InlineData("missing-source")]
    [InlineData("source-empty")]
    [InlineData("destination-occupied")]
    [InlineData("wrong-drone-station")]
    [InlineData("station-offline")]
    [InlineData("overweight")]
    [InlineData("range")]
    [InlineData("destination-reused")]
    [InlineData("source-other-ready")]
    public async Task Unsafe_resources_leave_everything_untouched(string condition)
    {
        _ = factory.CreateClient();
        Guid id = default;
        await factory.WithDbAsync(async db => {
            id = await SimulatorData.SeedAsync(db, factory.Clock.Now);
            var m = await db.FlightMissions.Include(x => x.Order).Include(x => x.Drone).SingleAsync(x => x.Id == id);
            switch (condition) {
                case "missing-source": m.StartSlotId = null; break;
                case "source-empty": (await db.StationSlots.FindAsync(m.StartSlotId))!.Status = "EMPTY"; break;
                case "destination-occupied": (await db.StationSlots.FindAsync(m.DestinationSlotId))!.Status = "OCCUPIED"; break;
                case "wrong-drone-station": m.Drone.CurrentStationId = m.Order.DestinationStationId; break;
                case "station-offline": (await db.LandingStations.FindAsync(m.Order.OriginStationId))!.Status = "MAINTENANCE"; break;
                case "overweight": m.Drone.MaxPayloadGram = 1; break;
                case "range": m.Drone.MaxFlightRangeKm = .001m; break;
                default:
                    var otherId = await SimulatorData.SeedAsync(db, factory.Clock.Now.AddHours(1));
                    var other = await db.FlightMissions.FindAsync(otherId);
                    if (condition == "destination-reused") other!.DestinationSlotId = m.DestinationSlotId;
                    else { other!.StartSlotId = m.StartSlotId; other.Order.OriginStationId = m.Order.OriginStationId; }
                    other!.CreatedAt = m.CreatedAt.AddMinutes(1);
                    break;
            }
            await db.SaveChangesAsync();
        });
        await SimulatorData.TickAsync(factory.Services);
        await CheckAsync(id, "READY_FOR_TAKEOFF", "PLANNED", 0, 0);
    }

    [Fact]
    public async Task Older_created_mission_can_take_off_from_a_slot_claimed_later_at_intake()
    {
        _ = factory.CreateClient();
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        Guid id = default;
        await factory.WithDbAsync(async db => id = await SimulatorData.IntakeAfterHistoricalUseAsync(db, factory.Clock.Now, actor.Id));
        await SimulatorData.TickAsync(factory.Services);
        await CheckAsync(id, "IN_TRANSIT", "IN_AIR", 1, 2);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(0, 0.000001, 1)]
    [InlineData(179.999, -179.999, 30)]
    public async Task Zero_short_and_longitude_wrapping_routes_arrive_safely(double origin, double destination, int seconds)
    {
        _ = factory.CreateClient();
        Guid id = default;
        await factory.WithDbAsync(async db => {
            id = await SimulatorData.SeedAsync(db, factory.Clock.Now);
            var m = await db.FlightMissions.Include(x => x.Order).ThenInclude(x => x.OriginStation)
                .Include(x => x.Order).ThenInclude(x => x.DestinationStation).SingleAsync(x => x.Id == id);
            m.Order.OriginStation.Longitude = (decimal)origin; m.Order.DestinationStation.Longitude = (decimal)destination;
            await db.SaveChangesAsync();
        });
        await SimulatorData.TickAsync(factory.Services);
        factory.Clock.Now = factory.Clock.Now.AddSeconds(seconds);
        await SimulatorData.TickAsync(factory.Services);
        await factory.WithDbAsync(async db => {
            Assert.Equal("COMPLETED", (await db.FlightMissions.FindAsync(id))!.FlightStatus);
            var final = await db.FlightTelemetryLogs.Where(x => x.MissionId == id).OrderByDescending(x => x.Timestamp).FirstAsync();
            Assert.Equal((decimal)destination, final.Longitude); Assert.Equal(0m, final.SpeedMps);
            Assert.All(await db.FlightTelemetryLogs.Where(x => x.MissionId == id).ToListAsync(), x => Assert.InRange(x.Longitude, -180m, 180m));
        });
    }

    [Fact]
    public async Task Rescheduled_order_with_prior_departure_starts_new_mission_and_preserves_aborted_history()
    {
        _ = factory.CreateClient();
        Guid id = default; Guid priorId = default;
        var previousStart = factory.Clock.Now.AddHours(-2).UtcDateTime;
        await factory.WithDbAsync(async db => {
            id = await SimulatorData.SeedAsync(db, factory.Clock.Now);
            var current = await db.FlightMissions.Include(m => m.Order).SingleAsync(m => m.Id == id);
            current.Order.ActualDepartureTime = previousStart; // failure resolution deliberately retains this history
            var prior = new FlightMission {
                OrderId = current.OrderId, FlightStatus = "ABORTED", StartTime = previousStart, EndTime = previousStart.AddMinutes(1),
                CreatedAt = previousStart.AddMinutes(-10),
                Drone = new Drone { SerialNumber = $"PRIOR-{Guid.NewGuid():N}", ModelName = "Prior carrier", Status = "FLYING", MaxPayloadGram = 3000, MaxFlightRangeKm = 15 }
            };
            db.FlightMissions.Add(prior); priorId = prior.Id;
            await db.SaveChangesAsync();
        });
        await SimulatorData.TickAsync(factory.Services);
        await CheckAsync(id, "IN_TRANSIT", "IN_AIR", 1, 1);
        await factory.WithDbAsync(async db => {
            var current = await db.FlightMissions.Include(m => m.Order).SingleAsync(m => m.Id == id);
            Assert.Equal(factory.Clock.Now.UtcDateTime, current.Order.ActualDepartureTime);
            var prior = await db.FlightMissions.Include(m => m.Drone).SingleAsync(m => m.Id == priorId);
            Assert.Equal("ABORTED", prior.FlightStatus); Assert.Equal(previousStart, prior.StartTime);
            Assert.Equal("FLYING", prior.Drone.Status); Assert.Null(prior.Drone.CurrentStationId);
        });
    }

    [Fact]
    public async Task Cancelled_tick_has_no_effects()
    {
        _ = factory.CreateClient();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SimulatorData.TickAsync(factory.Services, cancellation.Token));
    }

    private Task CheckAsync(Guid id, string order, string mission, int samples, int transitions) => factory.WithDbAsync(async db => {
        var m = await db.FlightMissions.Include(x => x.Order).SingleAsync(x => x.Id == id);
        Assert.Equal(order, m.Order.Status); Assert.Equal(mission, m.FlightStatus);
        Assert.Equal(samples, await db.FlightTelemetryLogs.CountAsync(x => x.MissionId == id));
        Assert.Equal(transitions, await db.DeliveryStatusLogs.CountAsync(x => x.OrderId == m.OrderId));
        Assert.Equal(transitions, await db.Notifications.CountAsync(x => x.OrderId == m.OrderId));
    });
}

public class SimulatorApiFactory : AuthApiFactory
{
    public SimulatorClock Clock { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["DroneSimulator:Enabled"] = "true"
        }));
        builder.ConfigureServices(services => {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(Clock);
            foreach (var descriptor in services.Where(x => x.ImplementationType?.FullName == "SmartDroneDelivery.Api.Services.DroneSimulatorWorker").ToList())
                services.Remove(descriptor);
        });
    }
}

public class SimulatorClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class SimulatorData
{
    public static async Task<Guid> IntakeAfterHistoricalUseAsync(AppDbContext db, DateTimeOffset now, Guid actorId)
    {
        var id = await SeedAsync(db, now);
        var current = await db.FlightMissions.Include(x => x.Order).SingleAsync(x => x.Id == id);
        var sourceId = current.StartSlotId!.Value;
        current.CreatedAt = now.AddHours(-2).UtcDateTime;
        current.StartSlotId = null; current.Order.Status = "APPROVED";
        (await db.StationSlots.FindAsync(sourceId))!.Status = "EMPTY";
        var historicalId = await SeedAsync(db, now.AddHours(-1));
        var historical = await db.FlightMissions.Include(x => x.Order).Include(x => x.Drone).SingleAsync(x => x.Id == historicalId);
        historical.CreatedAt = now.AddHours(-1).UtcDateTime;
        historical.FlightStatus = "COMPLETED"; historical.Order.Status = "DELIVERED";
        historical.StartTime = now.AddMinutes(-50).UtcDateTime; historical.EndTime = now.AddMinutes(-40).UtcDateTime;
        historical.Order.ActualDepartureTime = historical.StartTime; historical.Order.ActualDeliveryTime = now.AddMinutes(-30).UtcDateTime;
        (await db.StationSlots.FindAsync(historical.StartSlotId))!.Status = "EMPTY";
        (await db.StationSlots.FindAsync(historical.DestinationSlotId))!.Status = "EMPTY";
        historical.DestinationSlotId = sourceId; historical.Order.DestinationStationId = current.Order.OriginStationId;
        historical.Drone.Status = "CHARGING"; historical.Drone.CurrentStationId = current.Order.OriginStationId;
        await db.SaveChangesAsync();
        // Real intake claims the now-empty shared slot after the later-created historical
        // mission completed and its recipient collected their parcel.
        await new StationOperationService(db, new OrderTransitionWriter(db)).IntakeAsync(actorId, current.Order.OriginStationId, new() {
            TrackingNumber = current.Order.TrackingNumber, SlotId = sourceId, MeasuredWeightGram = 450,
            MeasuredLengthCm = 25, MeasuredWidthCm = 18, MeasuredHeightCm = 10, VisualInspectionPassed = true
        });
        return id;
    }

    public static async Task TickAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DroneSimulatorService>().TickAsync(ct);
    }

    public static async Task<Guid> SeedAsync(AppDbContext db, DateTimeOffset departure)
    {
        var id = await TelemetryTestData.SeedMissionAsync(db, "PLANNED");
        var m = await db.FlightMissions.Include(x => x.Order).ThenInclude(x => x.DestinationStation).Include(x => x.Drone).SingleAsync(x => x.Id == id);
        m.Order.Status = "READY_FOR_TAKEOFF";
        m.ScheduledDepartureTime = m.Order.ScheduledDepartureTime = departure.UtcDateTime;
        m.EstimatedArrivalTime = m.Order.EstimatedDeliveryTime = departure.AddMinutes(2).UtcDateTime;
        m.Order.DestinationStation.Longitude = .01m;
        m.Drone.Status = "ASSIGNED"; m.Drone.CurrentStationId = m.Order.OriginStationId;
        var source = new StationSlot { StationId = m.Order.OriginStationId, SlotNumber = 1, Status = "OCCUPIED" };
        var destination = new StationSlot { StationId = m.Order.DestinationStationId, SlotNumber = 1, Status = "RESERVED" };
        db.StationSlots.AddRange(source, destination); m.StartSlotId = source.Id; m.DestinationSlotId = destination.Id;
        await db.SaveChangesAsync(); return id;
    }
}
