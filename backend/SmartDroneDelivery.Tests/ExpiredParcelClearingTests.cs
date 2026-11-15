using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class ExpiredParcelClearingTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Theory]
    [InlineData("STATION_OPERATOR")]
    [InlineData("ADMIN")]
    public async Task Clear_releases_slot_audits_reason_and_preserves_failed_order_and_episode(string role)
    {
        var s = await ExpiredParcelData.SeedAsync(factory.Services, role);
        using var client = factory.CreateClient();
        using var response = await ExpiredParcelData.ClearAsync(client, s, "  Moved parcel to holding rack.  ");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(s.Order.Id, result.GetProperty("orderId").GetGuid());
        Assert.Equal(s.SlotId, result.GetProperty("slotId").GetGuid());
        Assert.Equal("EMPTY", result.GetProperty("slotStatus").GetString());
        Assert.True(result.GetProperty("clearedAt").GetDateTime() > DateTime.UtcNow.AddMinutes(-1));
        await factory.WithDbAsync(async db => {
            Assert.Equal("EMPTY", (await db.StationSlots.FindAsync(s.SlotId))!.Status);
            var order = await db.DeliveryOrders.FindAsync(s.Order.Id);
            Assert.Equal("FAILED", order!.Status);
            Assert.Null(order.ActualDeliveryTime);
            Assert.Equal(PickupTimeoutService.CancelReason, order.CancelReason);
            var log = Assert.Single(await db.DeliveryStatusLogs.Where(l => l.OrderId == order.Id).ToListAsync());
            Assert.Contains("Moved parcel to holding rack.", log.Remarks);
            Assert.Equal(PickupTimeoutService.LogRemarks, (await db.DeliveryFailureEpisodes.SingleAsync(e => e.FailureLogId == log.Id)).OriginalReason);
            var audit = Assert.Single(await db.AuditLogs.Where(a => a.EntityId == s.SlotId.ToString()).ToListAsync());
            Assert.Equal("EXPIRED_PARCEL_CLEARED", audit.Action);
            Assert.Equal(s.Actor.Id, audit.UserId);
            Assert.Equal("Moved parcel to holding rack.", db.Entry(audit).Property("Reason").CurrentValue);
            Assert.Equal("COMPLETED", (await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id)).FlightStatus);
        });
        using var availability = await SchedulingData.Send(client, s.Actor.Token, $"/api/landing-stations/{s.Order.DestinationStationId}/availability", HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, availability.StatusCode);
        Assert.Equal(1, (await availability.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("emptySlots").GetInt32());
        using var repeat = await ExpiredParcelData.ClearAsync(client, s);
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("CUSTOMER", HttpStatusCode.Forbidden)]
    [InlineData("DISPATCHER", HttpStatusCode.Forbidden)]
    [InlineData("STATION_OPERATOR", HttpStatusCode.Forbidden)]
    public async Task Caller_without_destination_permission_cannot_clear(string? role, HttpStatusCode expected)
    {
        var s = await ExpiredParcelData.SeedAsync(factory.Services);
        var token = role is null ? null : (await ReviewData.UserAsync(factory.Services, role)).Token;
        using var client = factory.CreateClient();
        using var response = await SchedulingData.Send(client, token, ExpiredParcelData.Path(s), HttpMethod.Post, new { slotId = s.SlotId, reason = "Moved to rack" });
        Assert.Equal(expected, response.StatusCode);
        await ExpiredParcelData.AssertOccupiedAsync(factory.Services, s);
    }

    [Theory]
    [InlineData("INACTIVE", "STATION_OPERATOR")]
    [InlineData("ACTIVE", "CUSTOMER")]
    public async Task Persisted_account_status_and_role_override_old_token(string status, string role)
    {
        var s = await ExpiredParcelData.SeedAsync(factory.Services);
        await factory.WithDbAsync(async db => {
            var actor = (await db.Users.FindAsync(s.Actor.Id))!;
            actor.Status = status;
            actor.RoleId = (await db.Roles.SingleAsync(r => r.Code == role)).Id;
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        using var response = await ExpiredParcelData.ClearAsync(client, s);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await ExpiredParcelData.AssertOccupiedAsync(factory.Services, s);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("abcd")]
    public async Task Invalid_reason_is_rejected_without_effects(string? reason)
    {
        var s = await ExpiredParcelData.SeedAsync(factory.Services);
        using var client = factory.CreateClient();
        using var response = await ExpiredParcelData.ClearAsync(client, s, reason);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await ExpiredParcelData.AssertOccupiedAsync(factory.Services, s);
    }

    [Fact]
    public async Task Reason_over_500_and_missing_or_empty_slot_are_rejected()
    {
        var s = await ExpiredParcelData.SeedAsync(factory.Services);
        using var client = factory.CreateClient();
        foreach (var body in new object[] { new { slotId = s.SlotId, reason = new string('x', 501) }, new { reason = "Moved to rack" }, new { slotId = Guid.Empty, reason = "Moved to rack" } }) {
            using var response = await SchedulingData.Send(client, s.Actor.Token, ExpiredParcelData.Path(s), HttpMethod.Post, body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        await ExpiredParcelData.AssertOccupiedAsync(factory.Services, s);
    }

    [Theory]
    [InlineData("station")]
    [InlineData("slot")]
    public async Task Missing_resource_returns_404(string missing)
    {
        var s = await ExpiredParcelData.SeedAsync(factory.Services, "ADMIN");
        using var client = factory.CreateClient();
        var path = missing == "station" ? $"/api/landing-stations/{Guid.NewGuid()}/operations/clear-expired-parcel" : ExpiredParcelData.Path(s);
        using var response = await SchedulingData.Send(client, s.Actor.Token, path, HttpMethod.Post,
            new { slotId = missing == "slot" ? Guid.NewGuid() : s.SlotId, reason = "Moved to rack" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ExpiredParcelData.AssertOccupiedAsync(factory.Services, s);
    }

    [Theory]
    [InlineData("EMPTY", "FAILED")]
    [InlineData("RESERVED", "FAILED")]
    [InlineData("ERROR", "FAILED")]
    [InlineData("OCCUPIED", "ARRIVED")]
    [InlineData("OCCUPIED", "CANCELLED")]
    [InlineData("OCCUPIED", "APPROVED")]
    public async Task Invalid_slot_or_order_state_returns_409(string slotStatus, string orderStatus)
    {
        var s = await ExpiredParcelData.SeedAsync(factory.Services);
        await factory.WithDbAsync(async db => {
            (await db.StationSlots.FindAsync(s.SlotId))!.Status = slotStatus;
            (await db.DeliveryOrders.FindAsync(s.Order.Id))!.Status = orderStatus;
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        using var response = await ExpiredParcelData.ClearAsync(client, s);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("unlinked")]
    [InlineData("wrong-station")]
    [InlineData("not-timeout")]
    [InlineData("reused-destination")]
    [InlineData("reused-source")]
    public async Task Invalid_ownership_or_failure_kind_cannot_clear_a_parcel(string condition)
    {
        var s = await ExpiredParcelData.SeedAsync(factory.Services);
        var other = await ReviewData.OrderAsync(factory.Services, condition == "reused-source" ? "READY_FOR_TAKEOFF" : "ARRIVED", originId: s.Order.DestinationStationId);
        await factory.WithDbAsync(async db => {
            var mission = await db.FlightMissions.SingleAsync(m => m.OrderId == s.Order.Id);
            if (condition == "unlinked") mission.DestinationSlotId = null;
            if (condition == "wrong-station") (await db.StationSlots.FindAsync(s.SlotId))!.StationId = s.Order.OriginStationId;
            if (condition == "not-timeout") (await db.DeliveryFailureEpisodes.SingleAsync(e => e.FailureLog.OrderId == s.Order.Id)).FailureType = "HARDWARE_FAULT";
            if (condition.StartsWith("reused-")) db.FlightMissions.Add(new FlightMission {
                OrderId = other.Id, DroneId = mission.DroneId,
                DestinationSlotId = condition == "reused-destination" ? s.SlotId : null,
                StartSlotId = condition == "reused-source" ? s.SlotId : null,
                FlightStatus = condition == "reused-source" ? "PLANNED" : "COMPLETED",
                CreatedAt = mission.CreatedAt.AddMinutes(condition == "reused-source" ? -10 : 10)
            });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        using var response = await ExpiredParcelData.ClearAsync(client, s);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await ExpiredParcelData.AssertOccupiedAsync(factory.Services, s);
    }

    [Fact]
    public async Task Legacy_timeout_without_episode_metadata_can_be_cleared()
    {
        var s = await ExpiredParcelData.SeedAsync(factory.Services);
        await factory.WithDbAsync(async db => {
            db.DeliveryFailureEpisodes.Remove(await db.DeliveryFailureEpisodes.SingleAsync(e => e.FailureLog.OrderId == s.Order.Id));
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        using var response = await ExpiredParcelData.ClearAsync(client, s);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

internal static class ExpiredParcelData
{
    internal sealed record Scenario(DeliveryOrder Order, Guid SlotId, ReviewData.Actor Actor);
    public static async Task<Scenario> SeedAsync(IServiceProvider services, string role = "STATION_OPERATOR")
    {
        var actor = await ReviewData.UserAsync(services, role);
        var order = await ReviewData.OrderAsync(services, "FAILED");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var station = (await db.LandingStations.FindAsync(order.DestinationStationId))!;
        station.OperatorId = actor.Id; station.Status = "OPERATIONAL";
        var slot = new StationSlot { StationId = station.Id, SlotNumber = 1, Status = "OCCUPIED" };
        var drone = new Drone { SerialNumber = Guid.NewGuid().ToString("N"), ModelName = "Clearing test", CurrentStationId = station.Id, Status = "IDLE" };
        db.StationSlots.Add(slot);
        db.FlightMissions.Add(new FlightMission { OrderId = order.Id, Drone = drone, DestinationSlot = slot, FlightStatus = "COMPLETED", CreatedAt = DateTime.UtcNow.AddDays(-2), EndTime = DateTime.UtcNow.AddHours(-25) });
        var log = new DeliveryStatusLog { OrderId = order.Id, FromStatus = "ARRIVED", ToStatus = "FAILED", Remarks = PickupTimeoutService.LogRemarks, CreatedAt = DateTime.UtcNow.AddHours(-1) };
        db.DeliveryStatusLogs.Add(log);
        FailureRecording.Record(db, log, "COLLECTION_TIMEOUT", "PICKUP_TIMEOUT");
        (await db.DeliveryOrders.FindAsync(order.Id))!.CancelReason = PickupTimeoutService.CancelReason;
        await db.SaveChangesAsync();
        return new(order, slot.Id, actor);
    }
    public static string Path(Scenario s) => $"/api/landing-stations/{s.Order.DestinationStationId}/operations/clear-expired-parcel";
    public static Task<HttpResponseMessage> ClearAsync(HttpClient client, Scenario s, string? reason = "Moved parcel to holding rack.") =>
        SchedulingData.Send(client, s.Actor.Token, Path(s), HttpMethod.Post, new { slotId = s.SlotId, reason });
    public static async Task AssertOccupiedAsync(IServiceProvider services, Scenario s)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(s.SlotId))!.Status);
        Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "EXPIRED_PARCEL_CLEARED" && a.EntityId == s.SlotId.ToString()));
    }
}
