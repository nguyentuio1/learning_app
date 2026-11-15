using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class DroneSimulatorScenarioTests
{
    [Fact]
    public async Task Loss_preserves_last_reported_position_and_recovers_without_failing_order()
    {
        using var factory = new SimulatorApiFactory();
        using var app = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["DroneSimulator:TelemetryLossAfterSeconds"] = "1", ["DroneSimulator:TelemetryLossDurationSeconds"] = "70"
        })));
        using var client = app.CreateClient();
        var actor = await ReviewData.UserAsync(app.Services, "DISPATCHER");
        Guid id = default;
        await WithDb(app.Services, async db => id = await SimulatorData.SeedAsync(db, factory.Clock.Now));
        await SimulatorData.TickAsync(app.Services);
        var start = factory.Clock.Now;
        foreach (var elapsed in new[] { 1, 60, 61, 70 }) {
            factory.Clock.Now = start.AddSeconds(elapsed);
            await SimulatorData.TickAsync(app.Services);
            await WithDb(app.Services, async db => {
                var sample = Assert.Single(await db.FlightTelemetryLogs.Where(l => l.MissionId == id).ToListAsync());
                Assert.Equal(0m, sample.Longitude);
                Assert.Equal("IN_AIR", (await db.FlightMissions.FindAsync(id))!.FlightStatus);
            });
            using var response = await SchedulingData.Send(client, actor.Token, "/api/dispatch/active-deliveries", HttpMethod.Get);
            response.EnsureSuccessStatusCode();
            var row = Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
            Assert.Equal(elapsed > 60, row.GetProperty("telemetryStale").GetBoolean());
            Assert.Equal(start.UtcDateTime, row.GetProperty("lastTelemetryAt").GetDateTime());
        }
        factory.Clock.Now = start.AddSeconds(71);
        await SimulatorData.TickAsync(app.Services);
        await WithDb(app.Services, async db => {
            var samples = await db.FlightTelemetryLogs.Where(l => l.MissionId == id).OrderBy(l => l.Timestamp).ToListAsync();
            Assert.Equal(2, samples.Count); Assert.InRange(samples[1].Longitude, .007m, .009m);
            Assert.Empty(await db.DeliveryFailureEpisodes.ToListAsync());
        });
        using var recovered = await SchedulingData.Send(client, actor.Token, "/api/dispatch/active-deliveries", HttpMethod.Get);
        Assert.False(Assert.Single((await recovered.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()).GetProperty("telemetryStale").GetBoolean());
    }

    [Fact]
    public async Task Silent_abort_selects_third_nearest_station_and_recovery_survives_config_change_and_order_resolution()
    {
        using var factory = new SimulatorApiFactory(); _ = factory.CreateClient();
        var options = new DroneSimulatorOptions { Enabled = true, AbortAfterSeconds = 20,
            TelemetryLossAfterSeconds = 1, TelemetryLossDurationSeconds = 120 };
        Guid id = default, nearestId = default, orderId = default;
        await factory.WithDbAsync(async db => {
            id = await SimulatorData.SeedAsync(db, factory.Clock.Now);
            orderId = (await db.FlightMissions.FindAsync(id))!.OrderId;
            var nearest = new LandingStation { StationCode = $"DIV-{Guid.NewGuid():N}", Name = "Diversion pad", Status = "OPERATIONAL", Latitude = 0, Longitude = .002m };
            db.LandingStations.AddRange(nearest, new LandingStation { StationCode = $"OFF-{Guid.NewGuid():N}", Name = "Unavailable nearer pad", Status = "MAINTENANCE", Latitude = 0, Longitude = .00215m });
            nearestId = nearest.Id; await db.SaveChangesAsync();
        });
        await Tick(factory, options);
        factory.Clock.Now = factory.Clock.Now.AddSeconds(20);
        await Tick(factory, options);
        DateTime due = default;
        await factory.WithDbAsync(async db => {
            var diversion = await db.SimulatorDiversions.SingleAsync(d => d.MissionId == id);
            Assert.Equal(nearestId, diversion.LandingStationId); due = diversion.LandingDueAt!.Value;
            Assert.InRange(diversion.AbortLongitude, .0021m, .0022m);
            Assert.Single(await db.FlightTelemetryLogs.Where(l => l.MissionId == id).ToListAsync());
        });
        // Real resolution releases the reservation but must not free the still flying drone.
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<FailureService>().ResolveAsync(orderId, actor.Id, new("CANCEL", "Close the demonstration", null));
        options.AbortAfterSeconds = null; options.TelemetryLossAfterSeconds = null; options.TelemetryLossDurationSeconds = null;
        options.Enabled = false;
        factory.Clock.Now = new DateTimeOffset(due.AddSeconds(1));
        await Tick(factory, options);
        await factory.WithDbAsync(async db => Assert.Null((await db.SimulatorDiversions.FindAsync(id))!.LandedAt));
        options.Enabled = true;
        // New service/context with different flight configuration must honor the stored navigation deadline.
        await Tick(factory, options, .001);
        await Tick(factory, options, .001);
        await factory.WithDbAsync(async db => {
            var m = await db.FlightMissions.Include(m => m.Order).Include(m => m.Drone).SingleAsync(m => m.Id == id);
            Assert.Equal("CANCELLED", m.Order.Status); Assert.Equal("ABORTED", m.FlightStatus);
            Assert.Equal(nearestId, m.Drone.CurrentStationId); Assert.Equal("CHARGING", m.Drone.Status);
            Assert.Equal("EMPTY", (await db.StationSlots.FindAsync(m.DestinationSlotId))!.Status);
            Assert.NotNull((await db.SimulatorDiversions.FindAsync(id))!.LandedAt);
            Assert.Single(await db.DeliveryFailureEpisodes.ToListAsync());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Selected_station_becoming_unavailable_or_moving_cannot_produce_a_false_landing(bool moved)
    {
        using var factory = new SimulatorApiFactory(); _ = factory.CreateClient(); Guid id = default;
        var options = new DroneSimulatorOptions { Enabled = true, AbortAfterSeconds = 20 };
        await factory.WithDbAsync(async db => id = await SimulatorData.SeedAsync(db, factory.Clock.Now));
        await Tick(factory, options); factory.Clock.Now = factory.Clock.Now.AddSeconds(20); await Tick(factory, options);
        await factory.WithDbAsync(async db => {
            var diversion = await db.SimulatorDiversions.FindAsync(id);
            var station = await db.LandingStations.FindAsync(diversion!.LandingStationId);
            if (moved) station!.Longitude = .1m; else station!.Status = "MAINTENANCE";
            await db.SaveChangesAsync();
        });
        factory.Clock.Now = factory.Clock.Now.AddMinutes(1); await Tick(factory, options);
        await factory.WithDbAsync(async db => {
            Assert.Null((await db.SimulatorDiversions.FindAsync(id))!.LandedAt);
            var m = await db.FlightMissions.Include(m => m.Drone).SingleAsync(m => m.Id == id);
            Assert.Equal("FLYING", m.Drone.Status); Assert.Null(m.Drone.CurrentStationId);
        });
    }

    [Fact]
    public async Task Coarse_tick_that_first_observes_route_completion_does_not_retroactively_abort()
    {
        using var factory = new SimulatorApiFactory(); _ = factory.CreateClient(); Guid id = default;
        var options = new DroneSimulatorOptions { Enabled = true, AbortAfterSeconds = 20 };
        await factory.WithDbAsync(async db => id = await SimulatorData.SeedAsync(db, factory.Clock.Now));
        await Tick(factory, options); factory.Clock.Now = factory.Clock.Now.AddMinutes(2); await Tick(factory, options);
        await factory.WithDbAsync(async db => {
            Assert.Equal("COMPLETED", (await db.FlightMissions.FindAsync(id))!.FlightStatus);
            Assert.Empty(await db.SimulatorDiversions.ToListAsync()); Assert.Empty(await db.DeliveryFailureEpisodes.ToListAsync());
        });
    }

    [Fact]
    public async Task No_operational_station_records_unresolved_abort_then_starts_navigation_when_station_returns()
    {
        using var factory = new SimulatorApiFactory(); _ = factory.CreateClient();
        var options = new DroneSimulatorOptions { Enabled = true, AbortAfterSeconds = 20 };
        Guid id = default;
        await factory.WithDbAsync(async db => id = await SimulatorData.SeedAsync(db, factory.Clock.Now));
        await Tick(factory, options);
        await factory.WithDbAsync(async db => { foreach (var s in await db.LandingStations.ToListAsync()) s.Status = "MAINTENANCE"; await db.SaveChangesAsync(); });
        factory.Clock.Now = factory.Clock.Now.AddSeconds(20);
        await Tick(factory, options);
        await factory.WithDbAsync(async db => {
            var d = await db.SimulatorDiversions.SingleAsync(); Assert.Null(d.LandingStationId); Assert.Null(d.LandingDueAt);
            Assert.Equal("ABORTED", (await db.FlightMissions.FindAsync(id))!.FlightStatus);
        });
        factory.Clock.Now = factory.Clock.Now.AddHours(1);
        await factory.WithDbAsync(async db => {
            var m = await db.FlightMissions.Include(m => m.Order).SingleAsync(m => m.Id == id);
            (await db.LandingStations.FindAsync(m.Order.OriginStationId))!.Status = "OPERATIONAL"; await db.SaveChangesAsync();
        });
        await Tick(factory, options);
        await factory.WithDbAsync(async db => {
            var d = await db.SimulatorDiversions.SingleAsync(); Assert.Null(d.LandedAt);
            Assert.True(d.LandingDueAt > factory.Clock.Now.UtcDateTime);
            Assert.Equal(factory.Clock.Now.UtcDateTime, d.NavigationStartedAt);
        });
        factory.Clock.Now = factory.Clock.Now.AddSeconds(21);
        await Tick(factory, options);
        await factory.WithDbAsync(async db => Assert.NotNull((await db.SimulatorDiversions.SingleAsync()).LandedAt));
    }

    [Theory]
    [InlineData("PLANNED")]
    [InlineData("IN_AIR")]
    [InlineData("COMPLETED")]
    public async Task Recovery_never_overwrites_another_active_or_later_flown_mission(string newerStatus)
    {
        using var factory = new SimulatorApiFactory(); _ = factory.CreateClient();
        var options = new DroneSimulatorOptions { Enabled = true, AbortAfterSeconds = 20 };
        Guid id = default;
        await factory.WithDbAsync(async db => id = await SimulatorData.SeedAsync(db, factory.Clock.Now));
        await Tick(factory, options); factory.Clock.Now = factory.Clock.Now.AddSeconds(20); await Tick(factory, options);
        await factory.WithDbAsync(async db => {
            var prior = await db.FlightMissions.FindAsync(id);
            var otherId = await SimulatorData.SeedAsync(db, factory.Clock.Now.AddDays(1));
            var other = await db.FlightMissions.FindAsync(otherId);
            other!.DroneId = prior!.DroneId; other.FlightStatus = newerStatus;
            if (newerStatus != "PLANNED") other.StartTime = factory.Clock.Now.UtcDateTime;
            await db.SaveChangesAsync();
        });
        factory.Clock.Now = factory.Clock.Now.AddSeconds(30); await Tick(factory, options);
        await factory.WithDbAsync(async db => {
            Assert.Null((await db.SimulatorDiversions.FindAsync(id))!.LandedAt);
            var prior = await db.FlightMissions.Include(m => m.Drone).SingleAsync(m => m.Id == id);
            Assert.Equal("FLYING", prior.Drone.Status); Assert.Null(prior.Drone.CurrentStationId);
        });
    }

    [Theory]
    [InlineData("CANCELLED", "ABORTED")]
    [InlineData("ARRIVED", "COMPLETED")]
    public async Task Optional_abort_does_not_restart_terminal_missions(string status, string missionStatus)
    {
        using var factory = new SimulatorApiFactory(); _ = factory.CreateClient();
        await factory.WithDbAsync(async db => {
            var id = await SimulatorData.SeedAsync(db, factory.Clock.Now);
            var m = await db.FlightMissions.Include(m => m.Order).SingleAsync(m => m.Id == id);
            m.Order.Status = status; m.FlightStatus = missionStatus; await db.SaveChangesAsync();
        });
        await Tick(factory, new() { Enabled = true, AbortAfterSeconds = 0 });
        await factory.WithDbAsync(async db => { Assert.Empty(await db.SimulatorDiversions.ToListAsync()); Assert.Empty(await db.DeliveryStatusLogs.ToListAsync()); });
    }

    [Fact]
    public async Task Silence_from_takeoff_does_not_prevent_normal_landing_or_invent_received_samples()
    {
        using var factory = new SimulatorApiFactory(); _ = factory.CreateClient(); Guid id = default;
        var options = new DroneSimulatorOptions { Enabled = true, TelemetryLossAfterSeconds = 0, TelemetryLossDurationSeconds = 180 };
        await factory.WithDbAsync(async db => id = await SimulatorData.SeedAsync(db, factory.Clock.Now));
        await Tick(factory, options); factory.Clock.Now = factory.Clock.Now.AddMinutes(2); await Tick(factory, options);
        await factory.WithDbAsync(async db => {
            Assert.Equal("COMPLETED", (await db.FlightMissions.FindAsync(id))!.FlightStatus);
            Assert.Empty(await db.FlightTelemetryLogs.Where(l => l.MissionId == id).ToListAsync());
            Assert.Empty(await db.DeliveryFailureEpisodes.ToListAsync());
        });
    }

    internal static Task Tick(SimulatorApiFactory factory, DroneSimulatorOptions options, double speed = 12) => factory.WithDbAsync(db =>
        new DroneSimulatorService(db, new OrderTransitionWriter(db), new FlightTelemetryService(db, factory.Clock), factory.Clock,
            Options.Create(options), Options.Create(new DeliveryFlightOptions { CruiseSpeedMps = speed })).TickAsync());

    [Fact]
    public async Task Abort_records_once_and_only_later_reports_landing_at_nearest_station()
    {
        using var factory = new SimulatorApiFactory();
        using var app = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["DroneSimulator:AbortAfterSeconds"] = "20"
        })));
        _ = app.CreateClient(); Guid id = default;
        await WithDb(app.Services, async db => id = await SimulatorData.SeedAsync(db, factory.Clock.Now));
        await SimulatorData.TickAsync(app.Services);
        var start = factory.Clock.Now;
        factory.Clock.Now = start.AddSeconds(20);
        await SimulatorData.TickAsync(app.Services);
        await SimulatorData.TickAsync(app.Services);
        await WithDb(app.Services, async db => {
            var m = await db.FlightMissions.Include(m => m.Order).Include(m => m.Drone).SingleAsync(m => m.Id == id);
            Assert.Equal("FAILED", m.Order.Status); Assert.Equal("ABORTED", m.FlightStatus);
            Assert.Equal("FLYING", m.Drone.Status); Assert.Null(m.Drone.CurrentStationId);
            var episode = Assert.Single(await db.DeliveryFailureEpisodes.ToListAsync());
            Assert.Equal("MISSION_ABORTED", episode.FailureType); Assert.Equal("SIMULATOR_ABORT", episode.Source);
            Assert.Equal(2, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == m.OrderId));
            Assert.Equal(2, await db.Notifications.CountAsync(n => n.OrderId == m.OrderId));
        });
        factory.Clock.Now = start.AddSeconds(39);
        await SimulatorData.TickAsync(app.Services);
        await WithDb(app.Services, async db => Assert.Equal("FLYING", (await db.FlightMissions.Include(m => m.Drone).SingleAsync(m => m.Id == id)).Drone.Status));
        factory.Clock.Now = start.AddSeconds(41);
        await SimulatorData.TickAsync(app.Services);
        await SimulatorData.TickAsync(app.Services);
        await WithDb(app.Services, async db => {
            var m = await db.FlightMissions.Include(m => m.Order).Include(m => m.Drone).SingleAsync(m => m.Id == id);
            Assert.Equal("FAILED", m.Order.Status); Assert.Equal("ABORTED", m.FlightStatus);
            Assert.Equal("CHARGING", m.Drone.Status); Assert.Equal(m.Order.OriginStationId, m.Drone.CurrentStationId);
            Assert.Equal("RESERVED", (await db.StationSlots.FindAsync(m.DestinationSlotId))!.Status);
            Assert.Equal("EMPTY", (await db.StationSlots.FindAsync(m.StartSlotId))!.Status);
            Assert.Null(m.Order.ActualDeliveryTime);
            Assert.Single(await db.DeliveryFailureEpisodes.ToListAsync());
        });
    }

    internal static async Task WithDb(IServiceProvider services, Func<AppDbContext, Task> action)
    {
        using var scope = services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
