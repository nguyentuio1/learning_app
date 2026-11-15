using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class TrackingApiTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task Owner_gets_current_position_without_private_order_fields()
    {
        var data = await SeedAsync();
        using var response = await GetAsync(data.OrderId, data.Owner);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(data.OrderId, json.GetProperty("orderId").GetGuid());
        Assert.Equal("IN_TRANSIT", json.GetProperty("status").GetString());
        Assert.Equal(10.25m, json.GetProperty("lastPosition").GetProperty("latitude").GetDecimal());
        Assert.False(json.GetProperty("telemetryStale").GetBoolean());
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("593284", body);
        Assert.DoesNotContain("pickupSecureCode", body);
        Assert.DoesNotContain("droneSerialNumber", body);
        Assert.DoesNotContain("recipient", body);
    }

    [Fact]
    public async Task Foreign_customer_and_unknown_order_have_the_same_not_found_response()
    {
        var data = await SeedAsync();
        var other = await ActorAsync("CUSTOMER");
        using var foreign = await GetAsync(data.OrderId, other);
        using var missing = await GetAsync(Guid.NewGuid(), other);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal((await foreign.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString(),
            (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("MANAGER")]
    [InlineData("DISPATCHER")]
    public async Task Active_staff_can_track_another_customers_order(string role)
    {
        var data = await SeedAsync();
        using var response = await GetAsync(data.OrderId, await ActorAsync(role));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_and_station_operator_are_denied()
    {
        var data = await SeedAsync();
        using var anonymous = await GetAsync(data.OrderId, null);
        using var denied = await GetAsync(data.OrderId, await ActorAsync("STATION_OPERATOR"));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_token_does_not_bypass_account_deactivation_or_staff_demotion(bool demote)
    {
        var data = await SeedAsync();
        var staff = await ActorAsync("ADMIN");
        var token = Token(staff);
        await factory.WithDbAsync(async db => {
            var user = await db.Users.FindAsync(staff);
            if (demote) user!.RoleId = (await db.Roles.SingleAsync(r => r.Code == "CUSTOMER")).Id;
            else user!.Status = "INACTIVE";
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync($"/api/delivery-orders/{data.OrderId}/tracking");
        Assert.Equal(demote ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("IN_AIR", "IN_TRANSIT", true, false)]
    [InlineData("IN_AIR", "IN_TRANSIT", false, true)]
    [InlineData("COMPLETED", "ARRIVED", true, false)]
    [InlineData("ABORTED", "FAILED", true, false)]
    public async Task Position_and_staleness_follow_current_flight_state(string flight, string status, bool sample, bool stale)
    {
        var data = await SeedAsync(flight, status, sample);
        using var response = await GetAsync(data.OrderId, data.Owner);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(stale, json.GetProperty("telemetryStale").GetBoolean());
        Assert.Equal(sample && flight == "IN_AIR" ? JsonValueKind.Object : JsonValueKind.Null,
            json.GetProperty("lastPosition").ValueKind);
    }

    [Fact]
    public async Task Newest_mission_without_samples_does_not_reuse_an_older_flights_position()
    {
        var data = await SeedAsync();
        await factory.WithDbAsync(async db => {
            var old = await db.FlightMissions.SingleAsync(m => m.Id == data.MissionId);
            old.FlightStatus = "ABORTED";
            db.FlightMissions.Add(new FlightMission { OrderId = data.OrderId, DroneId = old.DroneId,
                FlightStatus = "IN_AIR", CreatedAt = old.CreatedAt.AddMinutes(1) });
            await db.SaveChangesAsync();
        });
        using var response = await GetAsync(data.OrderId, data.Owner);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, json.GetProperty("lastPosition").ValueKind);
        Assert.True(json.GetProperty("telemetryStale").GetBoolean());
    }

    internal async Task<(Guid OrderId, Guid MissionId, Guid Owner)> SeedAsync(
        string flight = "IN_AIR", string status = "IN_TRANSIT", bool sample = true)
    {
        (Guid OrderId, Guid MissionId, Guid Owner) result = default;
        await factory.WithDbAsync(async db => {
            var id = await TelemetryTestData.SeedMissionAsync(db, flight);
            var mission = await db.FlightMissions.Include(m => m.Order).SingleAsync(m => m.Id == id);
            mission.Order.Status = status;
            if (sample) db.FlightTelemetryLogs.Add(new FlightTelemetryLog { MissionId = id,
                Latitude = 10.25m, Longitude = 106.5m, AltitudeMeters = 80, SpeedMps = 12,
                BatteryPercentage = 95, Timestamp = DateTime.UtcNow.AddSeconds(-5) });
            await db.SaveChangesAsync();
            result = (mission.OrderId, id, mission.Order.CustomerId);
        });
        return result;
    }

    internal async Task<Guid> ActorAsync(string role)
    {
        var id = Guid.NewGuid();
        await factory.WithDbAsync(async db => {
            db.Users.Add(new User { Id = id, RoleId = (await db.Roles.SingleAsync(r => r.Code == role)).Id,
                Email = $"tracking-{id:N}@example.com", FullName = "Tracking viewer", Status = "ACTIVE" });
            await db.SaveChangesAsync();
        });
        return id;
    }

    internal string Token(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartDroneDelivery.Api.Data.AppDbContext>();
        var user = db.Users.Include(u => u.Role).Single(u => u.Id == userId);
        return scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, DateTime.UtcNow);
    }

    private async Task<HttpResponseMessage> GetAsync(Guid orderId, Guid? viewer)
    {
        using var client = factory.CreateClient();
        if (viewer.HasValue) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(viewer.Value));
        return await client.GetAsync($"/api/delivery-orders/{orderId}/tracking");
    }
}
