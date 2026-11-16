using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Tests;

public class ExternalIntegrationAcceptanceTests
{
    [Fact]
    public async Task Key_provisioning_and_external_endpoints_return_no_store_cache_control()
    {
        using var f = new IntegrationApiFactory();
        using var admin = f.CreateClient();
        var customer = await IntegrationApiTests.ActorAsync(f.Services, admin, "CUSTOMER");
        var body = await IntegrationApiTests.ResourcesAsync(f.Services, customer);
        await IntegrationApiTests.ActorAsync(f.Services, admin, "ADMIN");

        using var createdKeyResp = await admin.PostAsJsonAsync(
            IntegrationApiTests.Keys,
            IntegrationApiTests.KeyBody(customer, f.Clock));
        Assert.Equal(HttpStatusCode.Created, createdKeyResp.StatusCode);
        Assert.True(createdKeyResp.Headers.CacheControl?.NoStore);
        var createdKey = await createdKeyResp.Content.ReadFromJsonAsync<JsonElement>();
        var secret = createdKey.GetProperty("apiKey").GetString()!;

        using var listResp = await admin.GetAsync(IntegrationApiTests.Keys);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        Assert.True(listResp.Headers.CacheControl?.NoStore);

        using var ext = f.CreateClient();
        ext.DefaultRequestHeaders.Add("X-API-Key", secret);

        using var createOrderResp = await ext.PostAsJsonAsync(IntegrationApiTests.Orders, body);
        Assert.Equal(HttpStatusCode.Created, createOrderResp.StatusCode);
        Assert.True(createOrderResp.Headers.CacheControl?.NoStore);
        var orderId = (await createOrderResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var readResp = await ext.GetAsync($"{IntegrationApiTests.Orders}/{orderId}");
        Assert.Equal(HttpStatusCode.OK, readResp.StatusCode);
        Assert.True(readResp.Headers.CacheControl?.NoStore);

        using var trackingResp = await ext.GetAsync($"{IntegrationApiTests.Orders}/{orderId}/tracking");
        Assert.Equal(HttpStatusCode.OK, trackingResp.StatusCode);
        Assert.True(trackingResp.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("12345")]
    public async Task Invalid_uuid_paths_return_bad_request_and_unknown_key_revoke_returns_not_found(string invalidId)
    {
        using var f = new IntegrationApiFactory();
        using var admin = f.CreateClient();
        var customer = await IntegrationApiTests.ActorAsync(f.Services, admin, "CUSTOMER");
        await IntegrationApiTests.ActorAsync(f.Services, admin, "ADMIN");
        var key = await IntegrationApiTests.ProvisionAsync(admin, customer, f.Clock);

        using var invalidRevoke = await admin.DeleteAsync($"{IntegrationApiTests.Keys}/{invalidId}");
        Assert.Equal(HttpStatusCode.BadRequest, invalidRevoke.StatusCode);

        using var unknownRevoke = await admin.DeleteAsync($"{IntegrationApiTests.Keys}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, unknownRevoke.StatusCode);

        using var ext = f.CreateClient();
        ext.DefaultRequestHeaders.Add("X-API-Key", key.Secret);

        using var invalidRead = await ext.GetAsync($"{IntegrationApiTests.Orders}/{invalidId}");
        Assert.Equal(HttpStatusCode.BadRequest, invalidRead.StatusCode);

        using var invalidTracking = await ext.GetAsync($"{IntegrationApiTests.Orders}/{invalidId}/tracking");
        Assert.Equal(HttpStatusCode.BadRequest, invalidTracking.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sdd_invalid_unknown_secret")]
    public async Task Missing_blank_or_unknown_api_key_is_rejected_with_unauthorized(string? headerValue)
    {
        using var f = new IntegrationApiFactory();
        using var ext = f.CreateClient();
        if (headerValue is not null)
        {
            ext.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", headerValue);
        }

        using var readResp = await ext.GetAsync($"{IntegrationApiTests.Orders}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, readResp.StatusCode);

        using var trackingResp = await ext.GetAsync($"{IntegrationApiTests.Orders}/{Guid.NewGuid()}/tracking");
        Assert.Equal(HttpStatusCode.Unauthorized, trackingResp.StatusCode);
    }

    [Fact]
    public async Task External_api_key_cannot_authenticate_signalr_hub_negotiate()
    {
        using var f = new IntegrationApiFactory();
        using var admin = f.CreateClient();
        var customer = await IntegrationApiTests.ActorAsync(f.Services, admin, "CUSTOMER");
        await IntegrationApiTests.ActorAsync(f.Services, admin, "ADMIN");
        var key = await IntegrationApiTests.ProvisionAsync(admin, customer, f.Clock);

        using var ext = f.CreateClient();
        ext.DefaultRequestHeaders.Add("X-API-Key", key.Secret);

        using var negotiate = await ext.PostAsync("/hubs/tracking/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, negotiate.StatusCode);
    }

    [Theory]
    [InlineData("status", "IN_TRANSIT")]
    [InlineData("trackingNumber", "DD-999999-9999")]
    [InlineData("pickupSecureCode", "000000")]
    [InlineData("price", 1)]
    public async Task External_order_create_rejects_unknown_or_override_fields(string fieldName, object value)
    {
        using var f = new IntegrationApiFactory();
        using var admin = f.CreateClient();
        var customer = await IntegrationApiTests.ActorAsync(f.Services, admin, "CUSTOMER");
        var body = await IntegrationApiTests.ResourcesAsync(f.Services, customer);
        await IntegrationApiTests.ActorAsync(f.Services, admin, "ADMIN");
        var key = await IntegrationApiTests.ProvisionAsync(admin, customer, f.Clock);

        body[fieldName] = value;

        using var ext = f.CreateClient();
        ext.DefaultRequestHeaders.Add("X-API-Key", key.Secret);
        using var resp = await ext.PostAsJsonAsync(IntegrationApiTests.Orders, body);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Non_utc_expiry_timestamp_is_rejected_on_key_creation()
    {
        using var f = new IntegrationApiFactory();
        using var admin = f.CreateClient();
        var customer = await IntegrationApiTests.ActorAsync(f.Services, admin, "CUSTOMER");
        await IntegrationApiTests.ActorAsync(f.Services, admin, "ADMIN");

        var payload = JsonSerializer.Serialize(new
        {
            customerId = customer,
            name = "Warehouse Local Time",
            scopes = new[] { "orders:read" },
            expiresAt = $"{f.Clock.GetUtcNow().AddDays(30):yyyy-MM-ddTHH:mm:ss}+07:00"
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var resp = await admin.PostAsync(IntegrationApiTests.Keys, content);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task External_order_create_writes_initial_status_log_and_notification()
    {
        using var f = new IntegrationApiFactory();
        using var admin = f.CreateClient();
        var customer = await IntegrationApiTests.ActorAsync(f.Services, admin, "CUSTOMER");
        var body = await IntegrationApiTests.ResourcesAsync(f.Services, customer);
        await IntegrationApiTests.ActorAsync(f.Services, admin, "ADMIN");
        var key = await IntegrationApiTests.ProvisionAsync(admin, customer, f.Clock);

        using var ext = f.CreateClient();
        ext.DefaultRequestHeaders.Add("X-API-Key", key.Secret);
        using var created = await ext.PostAsJsonAsync(IntegrationApiTests.Orders, body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var orderId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await f.WithDbAsync(async db =>
        {
            var logs = await db.DeliveryStatusLogs
                .Where(l => l.OrderId == orderId)
                .ToListAsync();
            Assert.Single(logs);
            Assert.Equal("PENDING", logs[0].ToStatus);

            var notifications = await db.Notifications
                .Where(n => n.UserId == customer && n.OrderId == orderId)
                .ToListAsync();
            Assert.Single(notifications);
        });
    }

    [Fact]
    public async Task Key_rotation_keeps_replacement_active_while_revoking_old_key()
    {
        using var f = new IntegrationApiFactory();
        using var admin = f.CreateClient();
        var customer = await IntegrationApiTests.ActorAsync(f.Services, admin, "CUSTOMER");
        var body = await IntegrationApiTests.ResourcesAsync(f.Services, customer);
        await IntegrationApiTests.ActorAsync(f.Services, admin, "ADMIN");

        var oldKey = await IntegrationApiTests.ProvisionAsync(admin, customer, f.Clock);
        var newKey = await IntegrationApiTests.ProvisionAsync(admin, customer, f.Clock);

        using var oldClient = f.CreateClient();
        oldClient.DefaultRequestHeaders.Add("X-API-Key", oldKey.Secret);
        var orderId = await IntegrationApiTests.AssertCreateReadAsync(oldClient, body);

        using var revokeOld = await admin.DeleteAsync($"{IntegrationApiTests.Keys}/{oldKey.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revokeOld.StatusCode);

        using var oldDenied = await oldClient.GetAsync($"{IntegrationApiTests.Orders}/{orderId}");
        Assert.Equal(HttpStatusCode.Unauthorized, oldDenied.StatusCode);

        using var newClient = f.CreateClient();
        newClient.DefaultRequestHeaders.Add("X-API-Key", newKey.Secret);
        using var newAllowed = await newClient.GetAsync($"{IntegrationApiTests.Orders}/{orderId}");
        Assert.Equal(HttpStatusCode.OK, newAllowed.StatusCode);
    }

    [Fact]
    public async Task External_tracking_endpoint_reflects_live_telemetry_and_stale_state()
    {
        using var f = new IntegrationApiFactory();
        using var admin = f.CreateClient();
        var customer = await IntegrationApiTests.ActorAsync(f.Services, admin, "CUSTOMER");
        var body = await IntegrationApiTests.ResourcesAsync(f.Services, customer);
        await IntegrationApiTests.ActorAsync(f.Services, admin, "ADMIN");
        var key = await IntegrationApiTests.ProvisionAsync(admin, customer, f.Clock);

        using var ext = f.CreateClient();
        ext.DefaultRequestHeaders.Add("X-API-Key", key.Secret);
        using var created = await ext.PostAsJsonAsync(IntegrationApiTests.Orders, body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var orderId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await f.WithDbAsync(async db =>
        {
            var order = await db.DeliveryOrders.SingleAsync(o => o.Id == orderId);
            order.Status = "IN_TRANSIT";

            var drone = new Drone
            {
                SerialNumber = $"DRN-EXT-{Guid.NewGuid():N}"[..20],
                ModelName = "SD-X1",
                MaxPayloadGram = 5000m,
                MaxFlightRangeKm = 20m,
                BatteryCapacityMah = 10000,
                Status = "IN_FLIGHT"
            };
            db.Drones.Add(drone);

            var mission = new FlightMission
            {
                OrderId = orderId,
                DroneId = drone.Id,
                FlightStatus = "IN_AIR",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            };
            db.FlightMissions.Add(mission);

            db.FlightTelemetryLogs.Add(new FlightTelemetryLog
            {
                MissionId = mission.Id,
                Latitude = 10.762622m,
                Longitude = 106.660172m,
                AltitudeMeters = 45.5m,
                SpeedMps = 12.0m,
                BatteryPercentage = 85,
                Timestamp = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        });

        using var liveResp = await ext.GetAsync($"{IntegrationApiTests.Orders}/{orderId}/tracking");
        Assert.Equal(HttpStatusCode.OK, liveResp.StatusCode);
        var live = await liveResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("IN_TRANSIT", live.GetProperty("status").GetString());
        Assert.False(live.GetProperty("telemetryStale").GetBoolean());
        Assert.Equal(JsonValueKind.Object, live.GetProperty("lastPosition").ValueKind);
        Assert.Equal(10.762622m, live.GetProperty("lastPosition").GetProperty("latitude").GetDecimal());

        await f.WithDbAsync(async db =>
        {
            var telemetry = await db.FlightTelemetryLogs.SingleAsync(t => t.Mission.OrderId == orderId);
            telemetry.Timestamp = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        });

        using var staleResp = await ext.GetAsync($"{IntegrationApiTests.Orders}/{orderId}/tracking");
        Assert.Equal(HttpStatusCode.OK, staleResp.StatusCode);
        var stale = await staleResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(stale.GetProperty("telemetryStale").GetBoolean());
    }
}
