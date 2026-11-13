using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class FailureTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Theory]
    [InlineData("RETURN_REQUESTED", "CANCEL")]
    [InlineData("RETURN_REQUESTED", "RESCHEDULE")]
    [InlineData("RETURN_REQUESTED", "RETURN_TO_ORIGIN")]
    [InlineData("RETURNED_TO_ORIGIN", "CANCEL")]
    [InlineData("RETURNED_TO_ORIGIN", "RESCHEDULE")]
    [InlineData("RETURNED_TO_ORIGIN", "RETURN_TO_ORIGIN")]
    public async Task Intake_reason_that_matches_return_marker_remains_human_data_and_can_be_resolved(string reason, string action)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        using var client = factory.CreateClient();
        using var rejected = await SchedulingData.Send(client, actor.Token, $"/api/landing-stations/{order.OriginStationId}/operations/intake-reject", HttpMethod.Post, new { trackingNumber = order.TrackingNumber, reason });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var before = await FailureData.DetailAsync(client, actor.Token, order.Id);
        Assert.Equal("UNRESOLVED", before.GetProperty("latestFailure").GetProperty("resolutionStatus").GetString());
        Assert.Equal(JsonValueKind.Null, before.GetProperty("latestFailure").GetProperty("lastResolutionAction").ValueKind);
        Assert.Equal("NOT_REQUESTED", before.GetProperty("returnToOriginStatus").GetString());
        Assert.Equal(reason, before.GetProperty("latestFailure").GetProperty("failureReason").GetString());
        Assert.True(before.GetProperty("resolutionHistoryComplete").GetBoolean());
        Assert.Empty(before.GetProperty("resolutionHistory").EnumerateArray());
        foreach (var filter in new[] { "UNRESOLVED", "RETURN_REQUESTED" }) {
            using var queue = await SchedulingData.Send(client, actor.Token, $"/api/dispatch/failed-orders?searchKeyword={order.TrackingNumber}&resolutionStatus={filter}", HttpMethod.Get);
            Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
            Assert.Equal(filter == "UNRESOLVED" ? 1 : 0, (await queue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
        }
        using var resolved = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/resolve-failure", HttpMethod.Post, new { action, note = "Checked package", packageVerified = true });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        var after = await FailureData.DetailAsync(client, actor.Token, order.Id);
        Assert.Equal(reason, after.GetProperty("latestFailure").GetProperty("failureReason").GetString());
        Assert.Single(after.GetProperty("resolutionHistory").EnumerateArray());
        Assert.Equal(action == "RETURN_TO_ORIGIN" ? "REQUESTED" : "NOT_REQUESTED", after.GetProperty("returnToOriginStatus").GetString());
        await factory.WithDbAsync(async db => Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.EntityId == order.Id.ToString())));
    }

    [Fact]
    public async Task Destination_reused_as_current_source_is_not_owned_by_newer_created_historical_mission()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await FailureData.InAirAsync(factory.Services);
        var replacement = await ReviewData.OrderAsync(factory.Services, "READY_FOR_TAKEOFF", originId: order.DestinationStationId);
        using var client = factory.CreateClient();
        using var report = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = "MISSION_ABORTED", reason = "Stopped" });
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        await factory.WithDbAsync(async db => {
            var old = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id);
            // After failed-parcel clearing, an already-created assignment claims this source at intake.
            (await db.StationSlots.FindAsync(old.DestinationSlotId))!.Status = "OCCUPIED";
            var replacementDrone = new Drone { SerialNumber = Guid.NewGuid().ToString(), ModelName = "Replacement", Status = "ASSIGNED", CurrentStationId = replacement.OriginStationId };
            db.Drones.Add(replacementDrone);
            db.FlightMissions.Add(new() { OrderId = replacement.Id, Drone = replacementDrone, StartSlotId = old.DestinationSlotId, FlightStatus = "PLANNED", CreatedAt = old.CreatedAt.AddMinutes(-10) });
            db.DeliveryStatusLogs.Add(new() { OrderId = replacement.Id, FromStatus = "APPROVED", ToStatus = "READY_FOR_TAKEOFF", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        var detail = await FailureData.DetailAsync(client, actor.Token, order.Id);
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("parcelLocation").ValueKind);
        using var cancel = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/resolve-failure", HttpMethod.Post, new { action = "CANCEL", note = "Parcel already cleared" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        await factory.WithDbAsync(async db => { var old = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id); Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(old.DestinationSlotId))!.Status); });
    }

    [Fact]
    public async Task Queue_combines_filters_pages_stably_and_uses_failure_time_instead_of_order_creation()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var older = await ReviewData.OrderAsync(factory.Services, "FAILED", createdAt: DateTime.UtcNow.AddYears(-1));
        var tied = await ReviewData.OrderAsync(factory.Services, "FAILED", older.OriginStationId, older.DestinationStationId);
        var newest = await ReviewData.OrderAsync(factory.Services, "FAILED", older.OriginStationId, older.DestinationStationId);
        var timestamp = DateTime.UtcNow.AddHours(-1);
        await factory.WithDbAsync(async db => {
            foreach (var (order, at, type) in new[] { (older, timestamp, "HARDWARE_FAULT"), (tied, timestamp, "HARDWARE_FAULT"), (newest, timestamp.AddMinutes(1), "COLLECTION_TIMEOUT") }) {
                var log = new DeliveryStatusLog { OrderId = order.Id, FromStatus = "IN_TRANSIT", ToStatus = "FAILED", CreatedAt = at, Remarks = "Original reason" };
                db.DeliveryStatusLogs.Add(log); db.DeliveryFailureEpisodes.Add(new() { FailureLog = log, FailureType = type, Source = "TEST", OriginalReason = log.Remarks });
            }
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        var baseQuery = $"/api/dispatch/failed-orders?originStationId={older.OriginStationId}&destinationStationId={older.DestinationStationId}&failureType=HARDWARE_FAULT&resolutionStatus=UNRESOLVED&fromDate={timestamp:O}&toDate={timestamp:O}&pageSize=1";
        var expected = new[] { older.Id, tied.Id }.Order().ToArray();
        for (var page = 1; page <= 3; page++) {
            using var response = await SchedulingData.Send(client, actor.Token, baseQuery + "&page=" + page, HttpMethod.Get);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(2, json.GetProperty("totalCount").GetInt32()); Assert.Equal(2, json.GetProperty("totalPages").GetInt32());
            var rows = json.GetProperty("items").EnumerateArray().ToArray();
            if (page == 3) Assert.Empty(rows); else Assert.Equal(expected[page - 1], Assert.Single(rows).GetProperty("orderId").GetGuid());
        }
        using var noResults = await SchedulingData.Send(client, actor.Token, baseQuery + "&searchKeyword=not-found", HttpMethod.Get);
        var empty = await noResults.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(0, empty.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Return_request_notifies_origin_operator_and_customer_without_second_status_transition()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var stationOperator = await ReviewData.UserAsync(factory.Services, "STATION_OPERATOR");
        var order = await FailureData.InAirAsync(factory.Services);
        await factory.WithDbAsync(async db => { (await db.LandingStations.FindAsync(order.OriginStationId))!.OperatorId = stationOperator.Id; await db.SaveChangesAsync(); });
        using var client = factory.CreateClient();
        using var report = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = "MISSION_ABORTED", reason = "Stopped" });
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        using var request = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/resolve-failure", HttpMethod.Post, new { action = "RETURN_TO_ORIGIN", note = "Return checked package" });
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        await factory.WithDbAsync(async db => {
            Assert.Equal("RETURN_REQUESTED", (await db.DeliveryOrders.FindAsync(order.Id))!.CancelReason);
            Assert.Equal(1, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == order.Id));
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.OrderId == order.Id && n.UserId == stationOperator.Id));
            Assert.Equal(2, await db.Notifications.CountAsync(n => n.OrderId == order.Id && n.UserId == order.CustomerId));
        });
        foreach (var action in new[] { "RESCHEDULE", "CANCEL" }) {
            using var repeated = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/resolve-failure", HttpMethod.Post, new { action, note = "Another choice", packageVerified = true });
            Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        }
    }

    [Fact]
    public async Task Long_text_is_rejected_and_missing_or_nonfailed_orders_have_contract_errors()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        using var client = factory.CreateClient();
        foreach (var (path, method, body, expected) in new (string, HttpMethod, object?, HttpStatusCode)[] {
            ($"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = "HARDWARE_FAULT", reason = new string('x', 501) }, HttpStatusCode.BadRequest),
            ($"/api/delivery-orders/{order.Id}/resolve-failure", HttpMethod.Post, new { action = "CANCEL", note = new string('x', 501) }, HttpStatusCode.BadRequest),
            ($"/api/delivery-orders/{Guid.NewGuid()}/fail", HttpMethod.Post, new { failureType = "HARDWARE_FAULT", reason = "Valid" }, HttpStatusCode.NotFound),
            ($"/api/delivery-orders/{Guid.NewGuid()}/resolve-failure", HttpMethod.Post, new { action = "CANCEL", note = "Valid" }, HttpStatusCode.NotFound),
            ($"/api/delivery-orders/{order.Id}/resolve-failure", HttpMethod.Post, new { action = "CANCEL", note = "Valid" }, HttpStatusCode.Conflict),
            ($"/api/dispatch/failed-orders/{order.Id}", HttpMethod.Get, null, HttpStatusCode.NotFound),
            ("/api/dispatch/failed-orders/invalid-id", HttpMethod.Get, null, HttpStatusCode.BadRequest)
        }) {
            using var response = await SchedulingData.Send(client, actor.Token, path, method, body); Assert.Equal(expected, response.StatusCode);
        }
    }

    [Fact]
    public async Task Historical_source_link_does_not_claim_a_parcel_received_later_by_an_older_mission()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await FailureData.InAirAsync(factory.Services);
        var other = await ReviewData.OrderAsync(factory.Services, "ARRIVED", destinationId: order.OriginStationId);
        Guid sourceSlotId = Guid.NewGuid();
        await factory.WithDbAsync(async db => {
            db.StationSlots.Add(new() { Id = sourceSlotId, StationId = order.OriginStationId, SlotNumber = 1, Status = "EMPTY" });
            var mission = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id); mission.StartSlotId = sourceSlotId;
            db.FlightMissions.Add(new() { OrderId = other.Id, DroneId = mission.DroneId, DestinationSlotId = sourceSlotId, FlightStatus = "COMPLETED", CreatedAt = mission.CreatedAt.AddMinutes(-10), EndTime = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        using var fail = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = "MISSION_ABORTED", reason = "Stopped" });
        Assert.Equal(HttpStatusCode.OK, fail.StatusCode);
        await factory.WithDbAsync(async db => { (await db.StationSlots.FindAsync(sourceSlotId))!.Status = "OCCUPIED"; await db.SaveChangesAsync(); });
        using var cancel = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/resolve-failure", HttpMethod.Post, new { action = "CANCEL", note = "Close failed flight" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        await factory.WithDbAsync(async db => Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(sourceSlotId))!.Status));
    }

    [Fact]
    public async Task Legacy_return_confirmation_marker_is_read_without_fabricated_handling_events()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services, "CANCELLED");
        var failedAt = DateTime.UtcNow.AddHours(-1);
        await factory.WithDbAsync(async db => {
            (await db.DeliveryOrders.FindAsync(order.Id))!.CancelReason = "RETURNED_TO_ORIGIN";
            db.DeliveryStatusLogs.AddRange(new() { OrderId = order.Id, FromStatus = "ARRIVED", ToStatus = "FAILED", CreatedAt = failedAt },
                new() { OrderId = order.Id, FromStatus = "FAILED", ToStatus = "CANCELLED", CreatedAt = failedAt.AddMinutes(5) });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        var detail = await FailureData.DetailAsync(client, actor.Token, order.Id);
        Assert.Equal("CONFIRMED", detail.GetProperty("returnToOriginStatus").GetString());
        Assert.Equal("RETURN_TO_ORIGIN", detail.GetProperty("latestFailure").GetProperty("lastResolutionAction").GetString());
        Assert.Empty(detail.GetProperty("resolutionHistory").EnumerateArray());
        Assert.False(detail.GetProperty("resolutionHistoryComplete").GetBoolean());
    }

    [Fact]
    public async Task Trusted_staging_does_not_reenter_nonrelational_operation_gate()
    {
        var order = await FailureData.InAirAsync(factory.Services);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var writer = scope.ServiceProvider.GetRequiredService<IOrderTransitionWriter>();
        using var gate = await writer.EnterOperationAsync();
        var saved = (await db.DeliveryOrders.FindAsync(order.Id))!;
        await scope.ServiceProvider.GetRequiredService<FailureService>().StageInAirFailureAsync(saved, "MISSION_ABORTED", "Scenario abort", null, "SIMULATOR_ABORT", DateTime.UtcNow).WaitAsync(TimeSpan.FromSeconds(3));
        await factory.WithDbAsync(async outside => Assert.Equal("IN_TRANSIT", (await outside.DeliveryOrders.FindAsync(order.Id))!.Status));
        await db.SaveChangesAsync();
        await factory.WithDbAsync(async outside => { Assert.Equal("FAILED", (await outside.DeliveryOrders.FindAsync(order.Id))!.Status); Assert.Equal("SIMULATOR_ABORT", (await outside.DeliveryFailureEpisodes.SingleAsync(e => e.FailureLog.OrderId == order.Id)).Source); });
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("APPROVED")]
    [InlineData("READY_FOR_TAKEOFF")]
    [InlineData("ARRIVED")]
    [InlineData("DELIVERED")]
    [InlineData("CANCELLED")]
    [InlineData("IN_TRANSIT")]
    public async Task Manual_report_requires_both_in_transit_order_and_in_air_mission(string status)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await ReviewData.OrderAsync(factory.Services, status);
        using var client = factory.CreateClient();
        using var response = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = "MISSION_ABORTED", reason = "Must not change" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await ReviewData.AssertEffectsAsync(factory.Services, order.Id, status, 0, 0);
    }

    [Theory]
    [InlineData("bad", "Valid", true)]
    [InlineData("CANCEL", "  ", true)]
    [InlineData("RESCHEDULE", "Valid", false)]
    public async Task Resolution_validates_action_note_and_package_verification(string action, string note, bool packageVerified)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        using var client = factory.CreateClient();
        using var response = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{Guid.NewGuid()}/resolve-failure", HttpMethod.Post, new { action, note, packageVerified });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reused_historical_slot_neither_blocks_cancellation_nor_claims_parcel_location()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await FailureData.InAirAsync(factory.Services);
        var other = await ReviewData.OrderAsync(factory.Services, "ARRIVED");
        using var client = factory.CreateClient();
        using var report = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = "MISSION_ABORTED", reason = "Stopped" });
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        await factory.WithDbAsync(async db => {
            var m = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id);
            (await db.StationSlots.FindAsync(m.DestinationSlotId))!.Status = "OCCUPIED";
            db.FlightMissions.Add(new() { OrderId = other.Id, DroneId = m.DroneId, DestinationSlotId = m.DestinationSlotId, FlightStatus = "COMPLETED", CreatedAt = m.CreatedAt.AddMinutes(1) });
            await db.SaveChangesAsync();
        });
        var detail = await FailureData.DetailAsync(client, actor.Token, order.Id);
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("parcelLocation").ValueKind);
        using var cancel = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/resolve-failure", HttpMethod.Post, new { action = "CANCEL", note = "Close failure" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        await factory.WithDbAsync(async db => { var m = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id); Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(m.DestinationSlotId))!.Status); });
    }

    [Fact]
    public async Task Active_selector_uses_latest_persisted_sample_with_strict_sixty_second_boundary()
    {
        var now = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var timed = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FrozenTime(now)); }));
        var actor = await ReviewData.UserAsync(timed.Services, "DISPATCHER");
        var order = await FailureData.InAirAsync(timed.Services);
        using (var scope = timed.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var mission = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id);
            db.FlightTelemetryLogs.AddRange(new() { MissionId = mission.Id, Timestamp = now.UtcDateTime.AddSeconds(-60) }, new() { MissionId = mission.Id, Timestamp = now.UtcDateTime.AddMinutes(-2) });
            await db.SaveChangesAsync();
        }
        using var client = timed.CreateClient();
        using var response = await SchedulingData.Send(client, actor.Token, $"/api/dispatch/active-deliveries?stationId={order.DestinationStationId}", HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        Assert.False(row.GetProperty("telemetryStale").GetBoolean()); Assert.Equal(now.UtcDateTime.AddSeconds(-60), row.GetProperty("lastTelemetryAt").GetDateTime());
    }

    private sealed class FrozenTime(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    [Fact]
    public async Task Timeout_and_intake_rejection_create_complete_typed_history_without_duplicate_effects()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var timeout = await ReviewData.OrderAsync(factory.Services, "ARRIVED");
        var rejection = await ReviewData.OrderAsync(factory.Services, "APPROVED");
        await factory.WithDbAsync(async db => { db.DeliveryStatusLogs.Add(new() { OrderId = timeout.Id, FromStatus = "IN_TRANSIT", ToStatus = "ARRIVED", CreatedAt = DateTime.UtcNow.AddHours(-25) }); await db.SaveChangesAsync(); });
        using (var scope = factory.Services.CreateScope()) {
            await scope.ServiceProvider.GetRequiredService<IPickupTimeoutService>().ExpireOverdueAsync(DateTime.UtcNow);
            await scope.ServiceProvider.GetRequiredService<IStationOperationService>().RejectIntakeAsync(actor.Id, rejection.OriginStationId, new() { TrackingNumber = rejection.TrackingNumber, Reason = "Package damaged" });
        }
        using var client = factory.CreateClient();
        foreach (var (order, type, reason) in new[] { (timeout, "COLLECTION_TIMEOUT", PickupTimeoutService.LogRemarks), (rejection, "UNKNOWN", "Package damaged") }) {
            var detail = await FailureData.DetailAsync(client, actor.Token, order.Id);
            Assert.Equal(type, detail.GetProperty("latestFailure").GetProperty("failureType").GetString());
            Assert.Equal(reason, detail.GetProperty("latestFailure").GetProperty("failureReason").GetString());
            Assert.True(detail.GetProperty("resolutionHistoryComplete").GetBoolean());
            await factory.WithDbAsync(async db => Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityId == order.Id.ToString())));
        }
    }

    [Fact]
    public async Task Latest_episode_filter_preserves_prior_resolution_history_and_resolved_discovery()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await FailureData.InAirAsync(factory.Services);
        using var client = factory.CreateClient();
        using var first = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = "MISSION_ABORTED", reason = "First failure" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var resolved = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/resolve-failure", HttpMethod.Post, new { action = "RESCHEDULE", note = "Inspected", packageVerified = true });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        using var queue = await SchedulingData.Send(client, actor.Token, $"/api/dispatch/failed-orders?searchKeyword={order.TrackingNumber}&resolutionStatus=RESOLVED", HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
        Assert.Single((await queue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
        // A new persisted flight episode must replace the summary without erasing earlier handling events.
        await factory.WithDbAsync(async db => { var saved = (await db.DeliveryOrders.FindAsync(order.Id))!; saved.Status = "IN_TRANSIT"; var old = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id); db.FlightMissions.Add(new() { OrderId = order.Id, DroneId = old.DroneId, FlightStatus = "IN_AIR", CreatedAt = DateTime.UtcNow.AddSeconds(1) }); await db.SaveChangesAsync(); });
        using var second = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = "HARDWARE_FAULT", reason = "Second failure" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var detail = await FailureData.DetailAsync(client, actor.Token, order.Id);
        Assert.Equal("Second failure", detail.GetProperty("latestFailure").GetProperty("failureReason").GetString());
        Assert.Equal(2, detail.GetProperty("failureHistory").GetArrayLength());
        Assert.Single(detail.GetProperty("resolutionHistory").EnumerateArray());
        using var filtered = await SchedulingData.Send(client, actor.Token, $"/api/dispatch/failed-orders?searchKeyword={order.TrackingNumber}&failureType=MISSION_ABORTED&resolutionStatus=ALL", HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        Assert.Equal(0, (await filtered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("COLLECTION_TIMEOUT", "Valid")]
    [InlineData("bad", "Valid")]
    [InlineData("MISSION_ABORTED", " ")]
    public async Task Report_validates_type_and_trimmed_reason(string type, string reason)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        using var client = factory.CreateClient();
        using var response = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{Guid.NewGuid()}/fail", HttpMethod.Post, new { failureType = type, reason });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("INACTIVE", "DISPATCHER")]
    [InlineData("ACTIVE", "CUSTOMER")]
    public async Task Persisted_account_blocks_stale_failure_and_active_selector_tokens(string status, string role)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        await factory.WithDbAsync(async db => { var u = (await db.Users.FindAsync(actor.Id))!; u.Status = status; u.RoleId = (await db.Roles.SingleAsync(r => r.Code == role)).Id; await db.SaveChangesAsync(); });
        using var client = factory.CreateClient();
        foreach (var (path, method) in FailureData.Routes(Guid.NewGuid()).Append(("/api/dispatch/active-deliveries", HttpMethod.Get))) {
            using var response = await SchedulingData.Send(client, actor.Token, path, method, method == HttpMethod.Post ? new { } : null);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("MISSION_ABORTED")]
    [InlineData("EMERGENCY_LANDING")]
    [InlineData("HARDWARE_FAULT")]
    public async Task Report_persists_original_episode_and_preserves_flying_drone(string type)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await FailureData.InAirAsync(factory.Services);
        using var client = factory.CreateClient();
        using var report = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = type, reason = "  Motor warning  " });
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        var detail = await FailureData.DetailAsync(client, actor.Token, order.Id);
        Assert.Equal(type, detail.GetProperty("latestFailure").GetProperty("failureType").GetString());
        Assert.Equal("Motor warning", detail.GetProperty("latestFailure").GetProperty("failureReason").GetString());
        Assert.True(detail.GetProperty("resolutionHistoryComplete").GetBoolean());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("parcelLocation").ValueKind);
        Assert.DoesNotContain("pickup", detail.GetRawText(), StringComparison.OrdinalIgnoreCase);
        await factory.WithDbAsync(async db => {
            var mission = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id);
            Assert.Equal("ABORTED", mission.FlightStatus); Assert.NotNull(mission.EndTime);
            Assert.Equal("FLYING", (await db.Drones.FindAsync(mission.DroneId))!.Status);
            Assert.Null((await db.Drones.FindAsync(mission.DroneId))!.CurrentStationId);
        });
        await ReviewData.AssertEffectsAsync(factory.Services, order.Id, "FAILED", 1, 1);
        using var again = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = type, reason = "Repeated" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Theory]
    [InlineData("RESCHEDULE", "APPROVED")]
    [InlineData("CANCEL", "CANCELLED")]
    [InlineData("RETURN_TO_ORIGIN", "FAILED")]
    public async Task Resolution_preserves_episode_records_event_and_refuses_repeated_decision(string action, string status)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await FailureData.InAirAsync(factory.Services);
        using var client = factory.CreateClient();
        using var report = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = "HARDWARE_FAULT", reason = "Original reason" });
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        using var resolve = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/resolve-failure", HttpMethod.Post, new { action, note = "  Checked package  ", packageVerified = true });
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        var detail = await FailureData.DetailAsync(client, actor.Token, order.Id);
        Assert.Equal(status, detail.GetProperty("status").GetString());
        Assert.Equal("Original reason", detail.GetProperty("latestFailure").GetProperty("failureReason").GetString());
        Assert.Equal(action, detail.GetProperty("latestFailure").GetProperty("lastResolutionAction").GetString());
        Assert.Equal(action == "RETURN_TO_ORIGIN" ? "RETURN_REQUESTED" : "RESOLVED", detail.GetProperty("latestFailure").GetProperty("resolutionStatus").GetString());
        var history = detail.GetProperty("resolutionHistory").EnumerateArray().ToArray();
        Assert.Single(history); Assert.Equal("Checked package", history[0].GetProperty("note").GetString());
        Assert.Equal(actor.Id, history[0].GetProperty("performedBy").GetGuid());
        await factory.WithDbAsync(async db => {
            var mission = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id);
            Assert.Equal("FLYING", (await db.Drones.FindAsync(mission.DroneId))!.Status);
            Assert.Equal(action == "RETURN_TO_ORIGIN" ? "RESERVED" : "EMPTY", (await db.StationSlots.FindAsync(mission.DestinationSlotId))!.Status);
            if (action == "RESCHEDULE") { var saved = (await db.DeliveryOrders.FindAsync(order.Id))!; Assert.Null(saved.ScheduledDepartureTime); Assert.Null(saved.EstimatedDeliveryTime); }
        });
        using var repeat = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/resolve-failure", HttpMethod.Post, new { action, note = "Repeat", packageVerified = true });
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
    }

    [Theory]
    [InlineData("CANCEL")]
    [InlineData("RESCHEDULE")]
    public async Task Occupied_parcel_blocks_resolution_without_effects(string action)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await FailureData.InAirAsync(factory.Services);
        using var client = factory.CreateClient();
        using var report = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/fail", HttpMethod.Post, new { failureType = "MISSION_ABORTED", reason = "Stopped" });
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        await factory.WithDbAsync(async db => { var m = await db.FlightMissions.SingleAsync(m => m.OrderId == order.Id); (await db.StationSlots.FindAsync(m.DestinationSlotId))!.Status = "OCCUPIED"; await db.SaveChangesAsync(); });
        using var resolve = await SchedulingData.Send(client, actor.Token, $"/api/delivery-orders/{order.Id}/resolve-failure", HttpMethod.Post, new { action, note = "Cannot release", packageVerified = true });
        Assert.Equal(HttpStatusCode.Conflict, resolve.StatusCode);
        await ReviewData.AssertEffectsAsync(factory.Services, order.Id, "FAILED", 1, 1);
        var detail = await FailureData.DetailAsync(client, actor.Token, order.Id);
        Assert.Empty(detail.GetProperty("resolutionHistory").EnumerateArray());
        Assert.Equal(order.DestinationStationId, detail.GetProperty("parcelLocation").GetProperty("stationId").GetGuid());
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("CUSTOMER", HttpStatusCode.Forbidden)]
    [InlineData("MANAGER", HttpStatusCode.Forbidden)]
    [InlineData("STATION_OPERATOR", HttpStatusCode.Forbidden)]
    public async Task Failure_routes_enforce_staff_scope(string? role, HttpStatusCode expected)
    {
        var actor = role is null ? null : await ReviewData.UserAsync(factory.Services, role);
        using var client = factory.CreateClient();
        foreach (var (path, method) in FailureData.Routes(Guid.NewGuid())) {
            using var response = await SchedulingData.Send(client, actor?.Token, path, method, method == HttpMethod.Post ? new { } : null);
            Assert.Equal(expected, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("failureType=bad")]
    [InlineData("resolutionStatus=bad")]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("originStationId=bad")]
    [InlineData("fromDate=2026-01-01")]
    [InlineData("fromDate=2030-01-02T00:00:00Z&toDate=2030-01-01T00:00:00Z")]
    [InlineData("searchKeyword=123456789012345678901234567890123456789012345678901")]
    public async Task Queue_validates_contract_filters(string query)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        using var client = factory.CreateClient();
        using var response = await SchedulingData.Send(client, actor.Token, "/api/dispatch/failed-orders?" + query, HttpMethod.Get);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Legacy_unknown_uses_log_reason_without_inference_and_has_incomplete_history()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services, "FAILED");
        await factory.WithDbAsync(async db => { db.DeliveryStatusLogs.Add(new() { OrderId = order.Id, FromStatus = "IN_TRANSIT", ToStatus = "FAILED", Remarks = "HARDWARE_FAULT human prose" }); await db.SaveChangesAsync(); });
        using var client = factory.CreateClient();
        var detail = await FailureData.DetailAsync(client, actor.Token, order.Id);
        Assert.Equal("UNKNOWN", detail.GetProperty("latestFailure").GetProperty("failureType").GetString());
        Assert.False(detail.GetProperty("resolutionHistoryComplete").GetBoolean());
        Assert.Empty(detail.GetProperty("resolutionHistory").EnumerateArray());
    }

    [Fact]
    public async Task Active_selector_allows_manager_and_no_sample_is_explicitly_stale()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "MANAGER");
        var order = await FailureData.InAirAsync(factory.Services);
        using var client = factory.CreateClient();
        using var response = await SchedulingData.Send(client, actor.Token, $"/api/dispatch/active-deliveries?stationId={order.OriginStationId}", HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        Assert.Equal(order.Id, row.GetProperty("orderId").GetGuid());
        Assert.True(row.GetProperty("telemetryStale").GetBoolean());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("lastTelemetryAt").ValueKind);
        Assert.DoesNotContain("pickup", row.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }
}

internal static class FailureData
{
    public static IEnumerable<(string, HttpMethod)> Routes(Guid id) => new[] {
        ($"/api/delivery-orders/{id}/fail", HttpMethod.Post), ($"/api/delivery-orders/{id}/resolve-failure", HttpMethod.Post),
        ("/api/dispatch/failed-orders", HttpMethod.Get), ($"/api/dispatch/failed-orders/{id}", HttpMethod.Get) };
    public static async Task<DeliveryOrder> InAirAsync(IServiceProvider services)
    {
        var order = await ReviewData.OrderAsync(services, "IN_TRANSIT");
        var drone = await SchedulingData.ResourcesAsync(services, order);
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = (await db.DeliveryOrders.FindAsync(order.Id))!;
        saved.ScheduledDepartureTime = DateTime.UtcNow.AddMinutes(-3); saved.EstimatedDeliveryTime = DateTime.UtcNow.AddMinutes(5);
        var d = (await db.Drones.FindAsync(drone.Id))!; d.Status = "FLYING"; d.CurrentStationId = null;
        var slot = await db.StationSlots.FirstAsync(s => s.StationId == order.DestinationStationId); slot.Status = "RESERVED";
        db.FlightMissions.Add(new FlightMission { OrderId = order.Id, DroneId = drone.Id, DestinationSlotId = slot.Id, FlightStatus = "IN_AIR", StartTime = DateTime.UtcNow.AddMinutes(-3) });
        await db.SaveChangesAsync(); return order;
    }
    public static async Task<JsonElement> DetailAsync(HttpClient client, string token, Guid id)
    {
        using var response = await SchedulingData.Send(client, token, $"/api/dispatch/failed-orders/{id}", HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
