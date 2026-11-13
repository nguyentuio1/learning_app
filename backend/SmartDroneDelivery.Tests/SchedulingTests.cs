using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Tests;

public class SchedulingTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task Schedule_assign_and_read_preserve_approved_status_and_reserve_lowest_slot()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        var drone = await SchedulingData.ResourcesAsync(factory.Services, order);
        using var client = factory.CreateClient();
        var departure = DateTime.UtcNow.AddHours(2);
        using var schedule = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/schedule", HttpMethod.Put, new { scheduledDepartureTime = departure });
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);
        var times = await schedule.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(departure, times.GetProperty("scheduledDepartureTime").GetDateTime());
        Assert.True(times.GetProperty("estimatedDeliveryTime").GetDateTime() > departure);
        using var assigned = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/flight-mission", HttpMethod.Post, new { droneId = drone.Id });
        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        Assert.Equal($"/api/delivery-orders/{order.Id}/flight-mission", assigned.Headers.Location?.ToString());
        await factory.WithDbAsync(async db =>
        {
            var mission = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id);
            Assert.Equal("PLANNED", mission.FlightStatus);
            Assert.Null(mission.StartSlotId);
            var slot = (await db.StationSlots.FindAsync(mission.DestinationSlotId))!;
            Assert.Equal(1, slot.SlotNumber);
            Assert.Equal("RESERVED", slot.Status);
            Assert.Equal("ASSIGNED", (await db.Drones.FindAsync(drone.Id))!.Status);
        });
        await ReviewData.AssertEffectsAsync(factory.Services, order.Id, "APPROVED", 0, 1);
        using var detail = await SchedulingData.Send(client, actor.Token, $"/api/dispatch/orders/{order.Id}", HttpMethod.Get);
        var json = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ASSIGNED", json.GetProperty("assignmentStatus").GetString());
        Assert.Equal(drone.Id, json.GetProperty("currentMission").GetProperty("drone").GetProperty("id").GetGuid());
        Assert.DoesNotContain("pickup", json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        using var edit = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/schedule", HttpMethod.Put, new { scheduledDepartureTime = departure.AddHours(1) });
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("CUSTOMER", HttpStatusCode.Forbidden)]
    [InlineData("MANAGER", HttpStatusCode.Forbidden)]
    [InlineData("STATION_OPERATOR", HttpStatusCode.Forbidden)]
    public async Task All_routes_require_dispatch_role(string? role, HttpStatusCode status)
    {
        var actor = role is null ? null : await ReviewData.UserAsync(factory.Services, role);
        using var client = factory.CreateClient();
        foreach (var (path, method) in SchedulingData.Routes(Guid.NewGuid()))
        {
            using var response = await SchedulingData.Send(client, actor?.Token, path, method, new { });
            Assert.Equal(status, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("INACTIVE", "DISPATCHER")]
    [InlineData("ACTIVE", "CUSTOMER")]
    public async Task Stale_token_cannot_bypass_persisted_account(string status, string role)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        await factory.WithDbAsync(async db => { var user = (await db.Users.FindAsync(actor.Id))!; user.Status = status; user.RoleId = (await db.Roles.SingleAsync(r => r.Code == role)).Id; await db.SaveChangesAsync(); });
        using var client = factory.CreateClient();
        foreach (var (path, method) in SchedulingData.Routes(Guid.NewGuid()))
        {
            using var response = await SchedulingData.Send(client, actor.Token, path, method, new { });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2020-01-01T00:00:00Z")]
    [InlineData("2030-01-01T00:00:00+07:00")]
    [InlineData("2030-01-01T00:00:00")]
    public async Task Schedule_requires_future_explicit_UTC(string? value)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        using var client = factory.CreateClient();
        using var response = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/schedule", HttpMethod.Put, new { scheduledDepartureTime = value });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("scheduledDepartureTime", out _));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("assignmentStatus=bad")]
    [InlineData("originStationId=bad")]
    [InlineData("fromDate=2030-01-02T00:00:00Z&toDate=2030-01-01T00:00:00Z")]
    [InlineData("scheduledFrom=2030-01-02T00:00:00Z&scheduledTo=2030-01-01T00:00:00Z")]
    [InlineData("scheduledFrom=2030-01-02T00:00:00%2B07:00")]
    [InlineData("fromDate=bad")]
    [InlineData("toDate=2030-01-01")]
    [InlineData("scheduledTo=bad")]
    [InlineData("searchKeyword=123456789012345678901234567890123456789012345678901")]
    public async Task Queue_validates_filters(string query)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        using var client = factory.CreateClient();
        using var response = await SchedulingData.Send(client, actor.Token, "/api/dispatch/approved-orders?" + query, HttpMethod.Get);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("CANCELLED")]
    [InlineData("READY_FOR_TAKEOFF")]
    public async Task Scheduling_and_candidates_refuse_nonapproved_orders_but_detail_remains_readable(string status)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await ReviewData.OrderAsync(factory.Services, status);
        using var client = factory.CreateClient();
        using var schedule = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/schedule", HttpMethod.Put, new { scheduledDepartureTime = DateTime.UtcNow.AddHours(2) });
        using var candidates = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/available-drones", HttpMethod.Get);
        using var detail = await SchedulingData.Send(client, actor.Token, $"/api/dispatch/orders/{order.Id}", HttpMethod.Get);
        Assert.Equal(HttpStatusCode.Conflict, schedule.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, candidates.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var json = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(status, json.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("currentMission").ValueKind);
        Assert.Equal(25, json.GetProperty("package").GetProperty("lengthCm").GetDecimal());
        Assert.True(json.GetProperty("originStation").TryGetProperty("latitude", out _));
    }

    [Fact]
    public async Task Stored_AI_estimate_stays_consistent_after_scheduling_updates_order_timestamp()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        var drone = await SchedulingData.ResourcesAsync(factory.Services, order);
        await factory.WithDbAsync(async db => {
            db.AiEtaPredictions.Add(new AiEtaPrediction { OrderId = order.Id, CalculatedDistanceKm = 10, PredictedDurationMinutes = 24, CreatedAt = order.CreatedAt.AddMinutes(-1) });
            (await db.Drones.FindAsync(drone.Id))!.MaxFlightRangeKm = 12;
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        var departure = DateTime.UtcNow.AddHours(2);
        using var schedule = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/schedule", HttpMethod.Put, new { scheduledDepartureTime = departure });
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);
        var times = await schedule.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(24, times.GetProperty("estimatedFlightMinutes").GetInt32());
        Assert.Equal(departure.AddMinutes(24), times.GetProperty("estimatedDeliveryTime").GetDateTime());
        using var candidates = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/available-drones", HttpMethod.Get);
        var candidate = (await candidates.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Single(x => x.GetProperty("droneId").GetGuid() == drone.Id);
        Assert.Contains("RANGE_INSUFFICIENT", candidate.GetProperty("reasonsNotEligible").EnumerateArray().Select(x => x.GetString()));
        using var assign = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/flight-mission", HttpMethod.Post, new { droneId = drone.Id });
        Assert.Equal(HttpStatusCode.BadRequest, assign.StatusCode);
    }

    [Fact]
    public async Task Advisory_explains_all_eligibility_failures_and_never_reserves_resources()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        var drone = await SchedulingData.ResourcesAsync(factory.Services, order);
        var other = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        var departure = DateTime.UtcNow.AddHours(2);
        await factory.WithDbAsync(async db => {
            var o = (await db.DeliveryOrders.FindAsync(order.Id))!; o.ScheduledDepartureTime = departure; o.EstimatedDeliveryTime = departure.AddMinutes(5);
            var prior = (await db.DeliveryOrders.FindAsync(other.Id))!; prior.ScheduledDepartureTime = departure; prior.EstimatedDeliveryTime = departure.AddMinutes(5);
            var bad = new Drone { SerialNumber = "BAD-" + Guid.NewGuid().ToString("N"), ModelName = "Small", MaxPayloadGram = 1, MaxFlightRangeKm = 0.1m, Status = "CHARGING" };
            db.Drones.Add(bad); db.FlightMissions.Add(new FlightMission { OrderId = other.Id, Drone = bad, FlightStatus = "PLANNED" });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        using var response = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/available-drones", HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var candidates = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToArray();
        Assert.Equal(drone.Id, candidates[0].GetProperty("droneId").GetGuid());
        var badCandidate = candidates.Single(x => x.GetProperty("serialNumber").GetString()!.StartsWith("BAD-"));
        Assert.Equal(new[] { "NOT_IDLE", "PAYLOAD_EXCEEDED", "RANGE_INSUFFICIENT", "TIME_OVERLAP", "NOT_AT_ORIGIN" }, badCandidate.GetProperty("reasonsNotEligible").EnumerateArray().Select(x => x.GetString()));
        await factory.WithDbAsync(async db => {
            Assert.Equal("IDLE", (await db.Drones.FindAsync(drone.Id))!.Status);
            Assert.False(await db.FlightMissions.AnyAsync(m => m.OrderId == order.Id));
            Assert.All(await db.StationSlots.Where(s => s.StationId == order.DestinationStationId).ToListAsync(), s => Assert.Equal("EMPTY", s.Status));
        });
    }

    [Theory]
    [InlineData("weight", HttpStatusCode.BadRequest)]
    [InlineData("range", HttpStatusCode.BadRequest)]
    [InlineData("station", HttpStatusCode.Conflict)]
    [InlineData("origin", HttpStatusCode.Conflict)]
    [InlineData("slot", HttpStatusCode.Conflict)]
    [InlineData("idle", HttpStatusCode.Conflict)]
    [InlineData("schedule", HttpStatusCode.Conflict)]
    public async Task Assignment_rechecks_guards_without_partial_effects(string guard, HttpStatusCode expected)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        var drone = await SchedulingData.ResourcesAsync(factory.Services, order);
        await factory.WithDbAsync(async db => {
            var o = (await db.DeliveryOrders.FindAsync(order.Id))!; o.ScheduledDepartureTime = DateTime.UtcNow.AddHours(2); o.EstimatedDeliveryTime = o.ScheduledDepartureTime.Value.AddMinutes(2);
            var d = (await db.Drones.FindAsync(drone.Id))!;
            if (guard == "weight") d.MaxPayloadGram = 100;
            if (guard == "range") d.MaxFlightRangeKm = 0.1m;
            if (guard == "origin") d.CurrentStationId = null;
            if (guard == "idle") d.Status = "CHARGING";
            if (guard == "station") (await db.LandingStations.FindAsync(order.DestinationStationId))!.Status = "MAINTENANCE";
            if (guard == "slot") foreach (var s in await db.StationSlots.Where(s => s.StationId == o.DestinationStationId).ToListAsync()) s.Status = "OCCUPIED";
            if (guard == "schedule") o.ScheduledDepartureTime = null;
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        using var response = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/flight-mission", HttpMethod.Post, new { droneId = drone.Id });
        Assert.Equal(expected, response.StatusCode);
        await ReviewData.AssertEffectsAsync(factory.Services, order.Id, "APPROVED", 0, 0);
        await factory.WithDbAsync(async db => Assert.False(await db.FlightMissions.AnyAsync(m => m.OrderId == order.Id)));
    }

    [Fact]
    public async Task Historical_mission_is_not_current_and_does_not_prevent_reassignment_or_poison_overlap()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        var drone = await SchedulingData.ResourcesAsync(factory.Services, order);
        await factory.WithDbAsync(async db => { db.FlightMissions.Add(new FlightMission { OrderId = order.Id, DroneId = drone.Id, FlightStatus = "ABORTED" }); await db.SaveChangesAsync(); });
        using var client = factory.CreateClient();
        using var before = await SchedulingData.Send(client, actor.Token, $"/api/dispatch/orders/{order.Id}", HttpMethod.Get);
        Assert.Equal("UNASSIGNED", (await before.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assignmentStatus").GetString());
        using var schedule = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/schedule", HttpMethod.Put, new { scheduledDepartureTime = DateTime.UtcNow.AddHours(2) });
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);
        using var assign = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/flight-mission", HttpMethod.Post, new { droneId = drone.Id });
        Assert.Equal(HttpStatusCode.Created, assign.StatusCode);
        using var repeat = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/flight-mission", HttpMethod.Post, new { droneId = drone.Id });
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        await factory.WithDbAsync(async db => Assert.Equal(2, await db.FlightMissions.CountAsync(m => m.OrderId == order.Id)));
    }

    [Fact]
    public async Task Approved_queue_combines_filters_counts_after_filtering_and_ignores_history()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var created = DateTime.UtcNow.AddDays(-1);
        var a = await ReviewData.OrderAsync(factory.Services, "APPROVED", createdAt: created);
        var b = await ReviewData.OrderAsync(factory.Services, "APPROVED", a.OriginStationId, a.DestinationStationId, created);
        var c = await ReviewData.OrderAsync(factory.Services, "PENDING", a.OriginStationId, a.DestinationStationId, created);
        var drone = await SchedulingData.ResourcesAsync(factory.Services, a);
        var departure = DateTime.UtcNow.AddHours(2);
        var prefix = "LIT_%" + Guid.NewGuid().ToString("N")[..5];
        await factory.WithDbAsync(async db => {
            foreach (var id in new[] { a.Id, b.Id, c.Id }) { var o = (await db.DeliveryOrders.FindAsync(id))!; o.TrackingNumber = prefix + id.ToString("N")[..4]; o.ScheduledDepartureTime = departure; }
            db.FlightMissions.AddRange(new FlightMission { OrderId = a.Id, DroneId = drone.Id, FlightStatus = "ABORTED" }, new FlightMission { OrderId = b.Id, DroneId = drone.Id, FlightStatus = "PLANNED" });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        var query = $"originStationId={a.OriginStationId}&destinationStationId={a.DestinationStationId}&searchKeyword={Uri.EscapeDataString(" " + prefix.ToLowerInvariant() + " ")}&fromDate={created.AddMinutes(-1):O}&toDate={created.AddMinutes(1):O}&scheduledFrom={departure.AddMinutes(-1):O}&scheduledTo={departure.AddMinutes(1):O}";
        using var page = await SchedulingData.Send(client, actor.Token, "/api/dispatch/approved-orders?" + query + "&pageSize=1", HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var json = await page.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, json.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, json.GetProperty("totalPages").GetInt32());
        Assert.Equal(new[] { a.Id, b.Id }.OrderBy(id => id).First(), json.GetProperty("items")[0].GetProperty("orderId").GetGuid());
        using var unassigned = await SchedulingData.Send(client, actor.Token, "/api/dispatch/approved-orders?" + query + "&assignmentStatus=UNASSIGNED", HttpMethod.Get);
        var item = (await unassigned.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().Single();
        Assert.Equal(a.Id, item.GetProperty("orderId").GetGuid()); Assert.Equal(JsonValueKind.Null, item.GetProperty("missionId").ValueKind);
        using var beyond = await SchedulingData.Send(client, actor.Token, "/api/dispatch/approved-orders?" + query + "&page=200", HttpMethod.Get);
        Assert.Empty((await beyond.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Intake_requires_schedule_and_assignment_then_links_source_slot_and_cancel_releases_reservation()
    {
        var admin = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        var drone = await SchedulingData.ResourcesAsync(factory.Services, order);
        var source = new StationSlot { StationId = order.OriginStationId, SlotNumber = 1 };
        await factory.WithDbAsync(async db => { db.StationSlots.Add(source); await db.SaveChangesAsync(); });
        using var client = factory.CreateClient();
        var body = new { trackingNumber = order.TrackingNumber, slotId = source.Id, measuredWeightGram = 450, measuredLengthCm = 25, measuredWidthCm = 18, measuredHeightCm = 10, visualInspectionPassed = true };
        using var noAssignment = await SchedulingData.Send(client, admin.Token, $"/api/landing-stations/{order.OriginStationId}/operations/intake", HttpMethod.Post, body);
        Assert.Equal(HttpStatusCode.Conflict, noAssignment.StatusCode);
        await factory.WithDbAsync(async db => Assert.Equal("EMPTY", (await db.StationSlots.FindAsync(source.Id))!.Status));
        using var schedule = await SchedulingData.Send(client, admin.Token, $"/api/delivery-orders/{order.Id}/schedule", HttpMethod.Put, new { scheduledDepartureTime = DateTime.UtcNow.AddHours(2) });
        using var assigned = await SchedulingData.Send(client, admin.Token, $"/api/delivery-orders/{order.Id}/flight-mission", HttpMethod.Post, new { droneId = drone.Id });
        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        using var intake = await SchedulingData.Send(client, admin.Token, $"/api/landing-stations/{order.OriginStationId}/operations/intake", HttpMethod.Post, body);
        Assert.Equal(HttpStatusCode.OK, intake.StatusCode);
        await factory.WithDbAsync(async db => {
            Assert.Equal(source.Id, (await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id)).StartSlotId);
            Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(source.Id))!.Status);
        });
        var other = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        var otherDrone = await SchedulingData.ResourcesAsync(factory.Services, other);
        using var s2 = await SchedulingData.Send(client, admin.Token, $"/api/delivery-orders/{other.Id}/schedule", HttpMethod.Put, new { scheduledDepartureTime = DateTime.UtcNow.AddHours(2) });
        using var a2 = await SchedulingData.Send(client, admin.Token, $"/api/delivery-orders/{other.Id}/flight-mission", HttpMethod.Post, new { droneId = otherDrone.Id });
        var customer = await ReviewData.TokenAsync(factory.Services, other.CustomerId);
        using var cancel = await SchedulingData.Send(client, customer, $"/api/delivery-orders/{other.Id}/cancel", HttpMethod.Post, new { cancellationReason = "Changed plans" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        await factory.WithDbAsync(async db => {
            var mission = await db.FlightMissions.SingleAsync(m => m.OrderId == other.Id);
            Assert.Equal("ABORTED", mission.FlightStatus); Assert.NotNull(mission.EndTime);
            Assert.Equal("IDLE", (await db.Drones.FindAsync(otherDrone.Id))!.Status);
            Assert.Equal("EMPTY", (await db.StationSlots.FindAsync(mission.DestinationSlotId))!.Status);
        });
    }

    [Fact]
    public async Task Search_keyword_length_is_validated_after_trimming()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var keyword = new string('Q', 50);
        using var client = factory.CreateClient();
        using var response = await SchedulingData.Send(client, actor.Token,
            "/api/dispatch/approved-orders?searchKeyword=" + Uri.EscapeDataString(" " + keyword + " "), HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Completed_mission_overlap_uses_actual_interval_and_symmetric_closed_buffers(bool existingEarlier, bool atBoundary)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        var other = await ReviewData.OrderAsync(factory.Services, "DELIVERED");
        var drone = await SchedulingData.ResourcesAsync(factory.Services, order);
        var departure = DateTime.UtcNow.AddHours(2);
        await factory.WithDbAsync(async db => {
            var o = (await db.DeliveryOrders.FindAsync(order.Id))!;
            o.ScheduledDepartureTime = departure; o.EstimatedDeliveryTime = departure.AddMinutes(2);
            var history = (await db.DeliveryOrders.FindAsync(other.Id))!;
            history.ScheduledDepartureTime = departure; history.EstimatedDeliveryTime = departure.AddHours(1);
            var gap = TimeSpan.FromMinutes(35).Add(TimeSpan.FromSeconds(atBoundary ? 0 : 1));
            var start = existingEarlier ? departure - gap - TimeSpan.FromMinutes(10) : o.EstimatedDeliveryTime!.Value + gap;
            db.FlightMissions.Add(new FlightMission { OrderId = other.Id, DroneId = drone.Id, FlightStatus = "COMPLETED",
                StartTime = start, EndTime = start.AddMinutes(10) });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        using var response = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/available-drones", HttpMethod.Get);
        var candidate = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Single(x => x.GetProperty("droneId").GetGuid() == drone.Id);
        Assert.Equal(!atBoundary, candidate.GetProperty("isEligible").GetBoolean());
        using var assignment = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/flight-mission", HttpMethod.Post, new { droneId = drone.Id });
        Assert.Equal(atBoundary ? HttpStatusCode.Conflict : HttpStatusCode.Created, assignment.StatusCode);
    }

    [Fact]
    public async Task Unknown_ids_and_missing_drone_have_contract_errors()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        using var client = factory.CreateClient();
        foreach (var (path, method) in SchedulingData.Routes(Guid.NewGuid()).Where(r => !r.Item1.Contains("approved-orders"))) {
            using var response = await SchedulingData.Send(client, actor.Token, path, method,
                method == HttpMethod.Get ? null : new { droneId = Guid.NewGuid(), scheduledDepartureTime = DateTime.UtcNow.AddHours(2) });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var missing = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/flight-mission", HttpMethod.Post, new { });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var malformed = await SchedulingData.Send(client, actor.Token, "/api/dispatch/orders/not-a-uuid", HttpMethod.Get);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        using var empty = await SchedulingData.Send(client, actor.Token, $"/api/dispatch/approved-orders?originStationId={Guid.NewGuid()}", HttpMethod.Get);
        var page = await empty.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, page.GetProperty("totalPages").GetInt32());
        Assert.Empty(page.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Pickup_selects_latest_completed_mission_and_refuses_an_occupied_slot_reused_by_another_order()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        using var client = factory.CreateClient();
        foreach (var reused in new[] { false, true }) {
            var order = await ReviewData.OrderAsync(factory.Services, "ARRIVED");
            var drone = await SchedulingData.ResourcesAsync(factory.Services, order);
            var other = await ReviewData.OrderAsync(factory.Services, "ARRIVED");
            await factory.WithDbAsync(async db => {
                (await db.DeliveryOrders.FindAsync(order.Id))!.PickupSecureCode = "123456";
                var slots = await db.StationSlots.Where(s => s.StationId == order.DestinationStationId).OrderBy(s => s.SlotNumber).ToListAsync();
                slots[1].Status = "OCCUPIED";
                var time = DateTime.UtcNow.AddHours(-1);
                db.FlightMissions.AddRange(new FlightMission { OrderId = order.Id, DroneId = drone.Id, FlightStatus = "ABORTED", DestinationSlotId = slots[0].Id, CreatedAt = time },
                    new FlightMission { OrderId = order.Id, DroneId = drone.Id, FlightStatus = "COMPLETED", DestinationSlotId = slots[1].Id, CreatedAt = time.AddMinutes(5) });
                if (reused) db.FlightMissions.Add(new FlightMission { OrderId = other.Id, DroneId = drone.Id, FlightStatus = "COMPLETED", DestinationSlotId = slots[1].Id, CreatedAt = time.AddMinutes(10) });
                await db.SaveChangesAsync();
            });
            using var response = await SchedulingData.Send(client, actor.Token, $"/api/landing-stations/{order.DestinationStationId}/operations/pickup", HttpMethod.Post,
                new { trackingNumber = order.TrackingNumber, pickupSecureCode = "123456" });
            Assert.Equal(reused ? HttpStatusCode.Conflict : HttpStatusCode.OK, response.StatusCode);
            await factory.WithDbAsync(async db => {
                var slot = await db.StationSlots.SingleAsync(s => s.StationId == order.DestinationStationId && s.SlotNumber == 2);
                Assert.Equal(reused ? "OCCUPIED" : "EMPTY", slot.Status);
                Assert.Equal(reused ? "ARRIVED" : "DELIVERED", (await db.DeliveryOrders.FindAsync(order.Id))!.Status);
            });
        }
    }

    [Fact]
    public async Task Cancellation_ignores_historical_occupied_slot_claims_reused_by_other_parcels()
    {
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        var other = await ReviewData.OrderAsync(factory.Services, "ARRIVED");
        var drone = await SchedulingData.ResourcesAsync(factory.Services, order);
        await factory.WithDbAsync(async db => {
            var slot = await db.StationSlots.FirstAsync(s => s.StationId == order.DestinationStationId);
            slot.Status = "OCCUPIED";
            db.FlightMissions.AddRange(new FlightMission { OrderId = order.Id, DroneId = drone.Id, FlightStatus = "ABORTED", DestinationSlotId = slot.Id, CreatedAt = DateTime.UtcNow.AddHours(-2) },
                new FlightMission { OrderId = other.Id, DroneId = drone.Id, FlightStatus = "COMPLETED", DestinationSlotId = slot.Id, CreatedAt = DateTime.UtcNow.AddHours(-1) });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        using var response = await SchedulingData.Send(client, await ReviewData.TokenAsync(factory.Services, order.CustomerId),
            $"/api/delivery-orders/{order.Id}/cancel", HttpMethod.Post, new { cancellationReason = "Changed plans" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await factory.WithDbAsync(async db => Assert.Equal(1, await db.StationSlots.CountAsync(s => s.StationId == order.DestinationStationId && s.Status == "OCCUPIED")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Flight_configuration_refuses_nonpositive_or_nonfinite_cruise_speed(double speed)
    {
        using var invalid = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => {
            services.PostConfigure<DeliveryFlightOptions>(o => o.CruiseSpeedMps = speed);
            // Test registered options validation directly: failed deferred-host startup
            // can dispose its provider before surfacing the original validation error.
            // Keep production IValidateOptions registrations; remove only startup
            // execution in this invalid-config host. Production ValidateOnStart stays.
            services.RemoveAll<IStartupValidator>();
        }));
        Assert.NotEmpty(invalid.Services.GetServices<IValidateOptions<DeliveryFlightOptions>>());
        var options = invalid.Services.GetRequiredService<IOptions<DeliveryFlightOptions>>();
        var error = Assert.Throws<OptionsValidationException>(() => options.Value);
        Assert.Equal(typeof(DeliveryFlightOptions), error.OptionsType);
        Assert.Contains("DeliveryFlight:CruiseSpeedMps must be finite and greater than zero.", error.Failures);
    }

    [Fact]
    public async Task Invalid_AI_estimate_falls_back_to_geodesic_distance_and_configured_speed()
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<DeliveryFlightOptions>(o => o.CruiseSpeedMps = 6)));
        using var client = configured.CreateClient();
        var actor = await ReviewData.UserAsync(configured.Services, "ADMIN");
        var order = await ReviewData.OrderAsync(configured.Services, "APPROVED");
        await SchedulingData.ResourcesAsync(configured.Services, order);
        using (var scope = configured.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AiEtaPredictions.Add(new AiEtaPrediction { OrderId = order.Id, CalculatedDistanceKm = -1, PredictedDurationMinutes = 50 });
            await db.SaveChangesAsync();
        }
        using var response = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/schedule", HttpMethod.Put,
            new { scheduledDepartureTime = DateTime.UtcNow.AddHours(2) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estimatedFlightMinutes").GetInt32());
    }
}

internal static class SchedulingData
{
    public static IEnumerable<(string, HttpMethod)> Routes(Guid id) => new[] {
        ($"/api/delivery-orders/{id}/schedule", HttpMethod.Put), ($"/api/delivery-orders/{id}/flight-mission", HttpMethod.Post),
        ($"/api/delivery-orders/{id}/available-drones", HttpMethod.Get), ("/api/dispatch/approved-orders", HttpMethod.Get), ($"/api/dispatch/orders/{id}", HttpMethod.Get) };
    public static async Task<HttpResponseMessage> Send(HttpClient client, string? token, string path, HttpMethod method, object? body = null)
    {
        using var request = ReviewData.Request(path, token, method);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
    public static async Task<Drone> ResourcesAsync(IServiceProvider services, DeliveryOrder order)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var destination = (await db.LandingStations.FindAsync(order.DestinationStationId))!;
        destination.Latitude = 0.01m;
        var drone = new Drone { SerialNumber = Guid.NewGuid().ToString("N"), ModelName = "Test", CurrentStationId = order.OriginStationId, Status = "IDLE", MaxPayloadGram = 3000, MaxFlightRangeKm = 15, BatteryCapacityMah = 5000 };
        db.Drones.Add(drone);
        db.StationSlots.AddRange(new StationSlot { StationId = order.DestinationStationId, SlotNumber = 2 }, new StationSlot { StationId = order.DestinationStationId, SlotNumber = 1 });
        await db.SaveChangesAsync();
        return drone;
    }
}
