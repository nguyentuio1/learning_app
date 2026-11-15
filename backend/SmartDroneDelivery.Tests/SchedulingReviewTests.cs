using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Tests;

public class SchedulingReviewTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Theory]
    [InlineData(1000, HttpStatusCode.BadRequest)]
    [InlineData(500, HttpStatusCode.OK)]
    public async Task Intake_checks_measured_weight_against_assigned_payload_before_any_effects(decimal measuredWeight, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        var s = await AssignedAsync(client);
        using var response = await SchedulingData.Send(client, s.Actor.Token, $"/api/landing-stations/{s.Order.OriginStationId}/operations/intake", HttpMethod.Post,
            new { trackingNumber = s.Order.TrackingNumber, slotId = s.Source.Id, measuredWeightGram = measuredWeight,
                measuredLengthCm = 25, measuredWidthCm = 18, measuredHeightCm = 10, visualInspectionPassed = true });
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest) {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("errors").TryGetProperty("measuredWeightGram", out _));
        }
        var accepted = expected == HttpStatusCode.OK;
        await factory.WithDbAsync(async db => {
            var order = (await db.DeliveryOrders.FindAsync(s.Order.Id))!;
            var mission = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id);
            Assert.Equal(accepted ? "READY_FOR_TAKEOFF" : "APPROVED", order.Status);
            Assert.Equal("PLANNED", mission.FlightStatus); Assert.Null(mission.EndTime);
            Assert.Equal(accepted ? s.Source.Id : (Guid?)null, mission.StartSlotId);
            Assert.Equal(accepted ? "OCCUPIED" : "EMPTY", (await db.StationSlots.FindAsync(s.Source.Id))!.Status);
            Assert.Equal("RESERVED", (await db.StationSlots.FindAsync(mission.DestinationSlotId))!.Status);
            Assert.Equal("ASSIGNED", (await db.Drones.FindAsync(s.Drone.Id))!.Status);
            Assert.Equal(accepted ? 1 : 0, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == order.Id));
            Assert.Equal(accepted ? 1 : 0, await db.Notifications.CountAsync(n => n.OrderId == order.Id));
            Assert.Equal(accepted ? 2 : 1, await db.AuditLogs.CountAsync(a => a.EntityId == order.Id.ToString()));
        });
    }

    [Theory]
    [InlineData("COMPLETED", true)]
    [InlineData("COMPLETED", false)]
    [InlineData("ABORTED", true)]
    public async Task Completed_missions_without_actual_start_use_retained_interval_in_candidates_and_assignment(string historicalStatus, bool overlapping)
    {
        using var client = factory.CreateClient();
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        var history = await ReviewData.OrderAsync(factory.Services, "DELIVERED");
        var drone = await SchedulingData.ResourcesAsync(factory.Services, order);
        var departure = DateTime.UtcNow.AddHours(2);
        await factory.WithDbAsync(async db => {
            var current = (await db.DeliveryOrders.FindAsync(order.Id))!;
            current.ScheduledDepartureTime = departure; current.EstimatedDeliveryTime = departure.AddMinutes(2);
            var end = departure.AddMinutes(overlapping ? -20 : -36);
            db.FlightMissions.Add(new FlightMission { OrderId = history.Id, DroneId = drone.Id, FlightStatus = historicalStatus,
                ScheduledDepartureTime = end.AddMinutes(-10), EstimatedArrivalTime = end, StartTime = null, EndTime = null });
            await db.SaveChangesAsync();
        });
        var blocked = historicalStatus == "COMPLETED" && overlapping;
        using var candidates = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/available-drones", HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, candidates.StatusCode);
        var candidate = (await candidates.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Single(d => d.GetProperty("droneId").GetGuid() == drone.Id);
        var candidateEligible = candidate.GetProperty("isEligible").GetBoolean();
        var reasons = candidate.GetProperty("reasonsNotEligible").EnumerateArray().Select(x => x.GetString()).ToArray();
        using var assignment = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/flight-mission", HttpMethod.Post, new { droneId = drone.Id });
        // Exercise both HTTP paths before checking either result: one query prefilter
        // must not mask a missing correction in the other.
        Assert.Equal(blocked ? HttpStatusCode.Conflict : HttpStatusCode.Created, assignment.StatusCode);
        Assert.Equal(!blocked, candidateEligible);
        Assert.Equal(blocked, reasons.Contains("TIME_OVERLAP"));
        if (blocked) await factory.WithDbAsync(async db => {
            Assert.False(await db.FlightMissions.AnyAsync(m => m.OrderId == order.Id));
            Assert.Equal("IDLE", (await db.Drones.FindAsync(drone.Id))!.Status);
            Assert.All(await db.StationSlots.Where(s => s.StationId == order.DestinationStationId).ToListAsync(), s => Assert.Equal("EMPTY", s.Status));
            Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityId == order.Id.ToString()));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Assigned_intake_rejection_releases_reservation_but_never_clears_an_occupied_parcel(bool occupied)
    {
        using var client = factory.CreateClient();
        var s = await AssignedAsync(client);
        if (occupied) await factory.WithDbAsync(async db => {
            var mission = await db.FlightMissions.SingleAsync(m => m.OrderId == s.Order.Id);
            (await db.StationSlots.FindAsync(mission.DestinationSlotId))!.Status = "OCCUPIED";
            await db.SaveChangesAsync();
        });
        using var response = await SchedulingData.Send(client, s.Actor.Token, $"/api/landing-stations/{s.Order.OriginStationId}/operations/intake-reject", HttpMethod.Post,
            new { trackingNumber = s.Order.TrackingNumber, reason = "Seal damaged during inspection" });
        Assert.Equal(occupied ? HttpStatusCode.Conflict : HttpStatusCode.OK, response.StatusCode);
        await factory.WithDbAsync(async db => {
            var order = (await db.DeliveryOrders.FindAsync(s.Order.Id))!;
            var mission = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id);
            Assert.Equal(occupied ? "APPROVED" : "FAILED", order.Status);
            Assert.Equal(occupied ? "PLANNED" : "ABORTED", mission.FlightStatus);
            Assert.Equal(!occupied, mission.EndTime.HasValue);
            Assert.Equal(occupied ? "ASSIGNED" : "IDLE", (await db.Drones.FindAsync(s.Drone.Id))!.Status);
            Assert.Equal(occupied ? "OCCUPIED" : "EMPTY", (await db.StationSlots.FindAsync(mission.DestinationSlotId))!.Status);
            Assert.Equal("EMPTY", (await db.StationSlots.FindAsync(s.Source.Id))!.Status); Assert.Null(mission.StartSlotId);
            Assert.Equal(occupied ? 0 : 1, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == order.Id));
            Assert.Equal(occupied ? 0 : 1, await db.Notifications.CountAsync(n => n.OrderId == order.Id));
            Assert.Equal(occupied ? 1 : 2, await db.AuditLogs.CountAsync(a => a.EntityId == order.Id.ToString()));
        });
    }

    private async Task<(ReviewData.Actor Actor, DeliveryOrder Order, Drone Drone, StationSlot Source)> AssignedAsync(HttpClient client)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        var drone = await SchedulingData.ResourcesAsync(factory.Services, order);
        var source = new StationSlot { StationId = order.OriginStationId, SlotNumber = 1 };
        await factory.WithDbAsync(async db => {
            (await db.Drones.FindAsync(drone.Id))!.MaxPayloadGram = 500;
            db.StationSlots.Add(source); await db.SaveChangesAsync();
        });
        using var schedule = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/schedule", HttpMethod.Put,
            new { scheduledDepartureTime = DateTime.UtcNow.AddHours(2) });
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);
        using var assigned = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/flight-mission", HttpMethod.Post, new { droneId = drone.Id });
        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        return (actor, order, drone, source);
    }
}
