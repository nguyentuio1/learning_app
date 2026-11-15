using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class DroneSimulatorAcceptanceTests
{
    [Fact]
    public async Task External_destination_samples_do_not_land_the_drone_and_simulator_continues_its_elapsed_route()
    {
        using var factory = new SimulatorApiFactory();
        using var client = factory.CreateClient();
        var admin = await ReviewData.UserAsync(factory.Services, "ADMIN");
        Guid id = default;
        await factory.WithDbAsync(async db => id = await SimulatorData.SeedAsync(db, factory.Clock.Now));
        var start = factory.Clock.Now;
        await SimulatorData.TickAsync(factory.Services);
        factory.Clock.Now = start.AddSeconds(10);

        // Even a received sample claiming the destination with zero speed is only telemetry.
        var destination = Sample(factory.Clock.Now, .01m);
        destination.SpeedMps = 0; destination.AltitudeMeters = 0;
        using (var response = await Post(client, admin.Token, id, destination))
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var duplicate = Sample(factory.Clock.Now, .008m);
        duplicate.Timestamp = "2026-11-13T08:00:10Z";
        using (var response = await Post(client, admin.Token, id, duplicate))
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using (var response = await Post(client, admin.Token, id, Sample(start.AddSeconds(5), .009m)))
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await factory.WithDbAsync(async db => {
            var latest = await new FlightTelemetryService(db, factory.Clock).GetLatestAsync(id);
            Assert.Equal(.008m, latest.LastPosition!.Longitude);
            Assert.Equal(factory.Clock.Now.UtcDateTime, latest.LastTelemetryAt);
            var mission = await db.FlightMissions.Include(m => m.Order).Include(m => m.Drone).SingleAsync(m => m.Id == id);
            Assert.Equal("IN_TRANSIT", mission.Order.Status); Assert.Equal("IN_AIR", mission.FlightStatus);
            Assert.Equal("FLYING", mission.Drone.Status); Assert.Null(mission.Drone.CurrentStationId);
            Assert.Equal("RESERVED", (await db.StationSlots.FindAsync(mission.DestinationSlotId))!.Status);
            Assert.Null(mission.EndTime); Assert.Null(mission.Order.ActualDeliveryTime);
            Assert.Single(await db.DeliveryStatusLogs.Where(l => l.OrderId == mission.OrderId).ToListAsync());
            Assert.Single(await db.Notifications.Where(n => n.OrderId == mission.OrderId).ToListAsync());
        });
        await SimulatorData.TickAsync(factory.Services);
        await factory.WithDbAsync(async db => Assert.Equal(4, await db.FlightTelemetryLogs.CountAsync(l => l.MissionId == id)));

        // Route progress follows elapsed simulation time, not the externally reported destination.
        factory.Clock.Now = start.AddSeconds(20);
        await SimulatorData.TickAsync(factory.Services);
        await factory.WithDbAsync(async db => {
            var latest = await new FlightTelemetryService(db, factory.Clock).GetLatestAsync(id);
            Assert.InRange(latest.LastPosition!.Longitude, .0021m, .0022m);
            Assert.Equal(12m, latest.LastPosition.SpeedMps);
            Assert.Equal(5, await db.FlightTelemetryLogs.CountAsync(l => l.MissionId == id));
        });
        factory.Clock.Now = start.AddMinutes(2);
        await SimulatorData.TickAsync(factory.Services);
        using (var response = await Post(client, admin.Token, id, Sample(factory.Clock.Now, .01m)))
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await factory.WithDbAsync(async db => {
            var latest = await new FlightTelemetryService(db, factory.Clock).GetLatestAsync(id);
            Assert.Equal(TelemetryAvailability.NotInAir, latest.Availability); Assert.Null(latest.LastPosition);
            var mission = await db.FlightMissions.Include(m => m.Order).SingleAsync(m => m.Id == id);
            Assert.Equal("ARRIVED", mission.Order.Status); Assert.Equal("COMPLETED", mission.FlightStatus);
            Assert.Equal(6, await db.FlightTelemetryLogs.CountAsync(l => l.MissionId == id));
            Assert.Equal(2, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == mission.OrderId));
            Assert.Equal(2, await db.Notifications.CountAsync(n => n.OrderId == mission.OrderId));
            Assert.Null(mission.Order.ActualDeliveryTime);
        });
    }

    [Fact]
    public async Task Silent_flight_is_reported_as_missing_then_abort_is_visible_without_a_live_position_or_pickup_secret()
    {
        using var factory = new SimulatorApiFactory();
        using var client = factory.CreateClient();
        var admin = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var options = new DroneSimulatorOptions { Enabled = true, TelemetryLossAfterSeconds = 0, TelemetryLossDurationSeconds = 120, AbortAfterSeconds = 20 };
        Guid id = default, orderId = default;
        await factory.WithDbAsync(async db => {
            id = await SimulatorData.SeedAsync(db, factory.Clock.Now);
            orderId = (await db.FlightMissions.FindAsync(id))!.OrderId;
        });
        await DroneSimulatorScenarioTests.Tick(factory, options);
        using (var response = await SchedulingData.Send(client, admin.Token, "/api/dispatch/active-deliveries", HttpMethod.Get)) {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var orders = await response.Content.ReadFromJsonAsync<JsonElement>();
            var order = Assert.Single(orders.EnumerateArray());
            Assert.Equal(orderId, order.GetProperty("orderId").GetGuid());
            Assert.Equal(JsonValueKind.Null, order.GetProperty("lastTelemetryAt").ValueKind);
            Assert.True(order.GetProperty("telemetryStale").GetBoolean());
            AssertNoSecret(orders.GetRawText());
        }
        factory.Clock.Now = factory.Clock.Now.AddSeconds(20);
        await DroneSimulatorScenarioTests.Tick(factory, options);
        using (var response = await SchedulingData.Send(client, admin.Token, "/api/dispatch/active-deliveries", HttpMethod.Get)) {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        }
        var detail = await FailureData.DetailAsync(client, admin.Token, orderId);
        Assert.Equal("MISSION_ABORTED", detail.GetProperty("latestFailure").GetProperty("failureType").GetString());
        Assert.True(detail.GetProperty("resolutionHistoryComplete").GetBoolean());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("parcelLocation").ValueKind);
        AssertNoSecret(detail.GetRawText());
        using (var response = await Post(client, admin.Token, id, Sample(factory.Clock.Now, .002m))) {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            AssertNoSecret(await response.Content.ReadAsStringAsync());
        }
        await factory.WithDbAsync(async db => {
            var snapshot = await new FlightTelemetryService(db, factory.Clock).GetLatestAsync(id);
            Assert.Equal(TelemetryAvailability.NotInAir, snapshot.Availability);
            Assert.Null(snapshot.LastPosition); Assert.Null(snapshot.LastTelemetryAt);
            Assert.Empty(await db.FlightTelemetryLogs.Where(l => l.MissionId == id).ToListAsync());
            var history = await db.DeliveryStatusLogs.Where(l => l.OrderId == orderId).OrderBy(l => l.CreatedAt).ToListAsync();
            Assert.Equal(new[] { "IN_TRANSIT", "FAILED" }, history.Select(l => l.ToStatus));
            Assert.All(history, l => Assert.Null(l.ChangedBy));
            AssertNoSecret(JsonSerializer.Serialize(history.Select(l => new { l.Remarks, l.FromStatus, l.ToStatus })));
            AssertNoSecret(JsonSerializer.Serialize(await db.Notifications.Where(n => n.OrderId == orderId).Select(n => new { n.Title, n.Message }).ToListAsync()));
            AssertNoSecret(JsonSerializer.Serialize(await db.AuditLogs.Select(a => new { a.Action, a.EntityName, a.EntityId }).ToListAsync()));
            AssertNoSecret(JsonSerializer.Serialize(await db.DeliveryFailureEpisodes.Where(e => e.FailureLog.OrderId == orderId).Select(e => new { e.OriginalReason, e.Source }).ToListAsync()));
        });
    }

    internal static TelemetrySampleRequest Sample(DateTimeOffset time, decimal longitude) => new() {
        Latitude = 0, Longitude = longitude, AltitudeMeters = 80, SpeedMps = 12,
        BatteryPercentage = 90, Timestamp = time.ToString("O")
    };

    internal static Task<HttpResponseMessage> Post(HttpClient client, string token, Guid mission, TelemetrySampleRequest sample) =>
        SchedulingData.Send(client, token, $"/api/flight-missions/{mission}/telemetry", HttpMethod.Post, sample);

    private static void AssertNoSecret(string text) {
        Assert.DoesNotContain("593284", text);
        Assert.DoesNotContain("pickupSecureCode", text, StringComparison.OrdinalIgnoreCase);
    }
}
