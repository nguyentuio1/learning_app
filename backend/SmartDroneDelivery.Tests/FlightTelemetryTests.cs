using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class FlightTelemetryTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>, IDisposable
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly string PasswordHash = new PasswordHasher().Hash("Passw0rd123");
    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Active_admin_appends_a_sample_without_workflow_changes()
    {
        var actor = await NewActorAsync("ADMIN");
        var missionId = await NewMissionAsync();
        using var response = await PostAsync(missionId, actor.Token, ValidSample());
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        await factory.WithDbAsync(async db =>
        {
            var log = Assert.Single(await db.FlightTelemetryLogs.Where(x => x.MissionId == missionId).ToListAsync());
            Assert.Equal(10.7769m, log.Latitude);
            Assert.Equal(71, log.BatteryPercentage);
            var mission = await db.FlightMissions.Include(x => x.Order).SingleAsync(x => x.Id == missionId);
            Assert.Equal("IN_AIR", mission.FlightStatus);
            Assert.Equal("IN_TRANSIT", mission.Order.Status);
            Assert.Equal(0, await db.DeliveryStatusLogs.CountAsync(x => x.OrderId == mission.OrderId));
            Assert.Equal(0, await db.Notifications.CountAsync(x => x.OrderId == mission.OrderId));
        });
    }

    [Fact]
    public async Task Anonymous_request_is_unauthorized()
    {
        using var response = await PostAsync(Guid.NewGuid(), null, ValidSample());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("CUSTOMER")]
    [InlineData("DISPATCHER")]
    [InlineData("MANAGER")]
    [InlineData("STATION_OPERATOR")]
    public async Task Other_roles_cannot_ingest_telemetry(string role)
    {
        var actor = await NewActorAsync(role);
        var mission = await NewMissionAsync();
        using var response = await PostAsync(mission, actor.Token, ValidSample());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertNoSamplesAsync(mission);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inactive_or_demoted_admin_with_an_existing_token_is_forbidden(bool demote)
    {
        var actor = await NewActorAsync("ADMIN");
        var mission = await NewMissionAsync();
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.FindAsync(actor.Id);
            if (demote) user!.RoleId = (await db.Roles.SingleAsync(x => x.Code == "CUSTOMER")).Id;
            else user!.Status = "INACTIVE";
            await db.SaveChangesAsync();
        });
        using var response = await PostAsync(mission, actor.Token, ValidSample());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertNoSamplesAsync(mission);
    }

    [Fact]
    public async Task Unknown_mission_returns_not_found()
    {
        var actor = await NewActorAsync("ADMIN");
        using var response = await PostAsync(Guid.NewGuid(), actor.Token, ValidSample());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("PLANNED")]
    [InlineData("COMPLETED")]
    [InlineData("ABORTED")]
    public async Task Mission_outside_in_air_returns_conflict_without_a_sample(string status)
    {
        var actor = await NewActorAsync("ADMIN");
        var mission = await NewMissionAsync(status);
        using var response = await PostAsync(mission, actor.Token, ValidSample());
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertNoSamplesAsync(mission);
    }

    [Theory]
    [InlineData("latitude", "-90.000001")]
    [InlineData("latitude", "90.000001")]
    [InlineData("longitude", "-180.000001")]
    [InlineData("longitude", "180.000001")]
    [InlineData("altitudeMeters", "-0.01")]
    [InlineData("altitudeMeters", "10000")]
    [InlineData("speedMps", "-0.01")]
    [InlineData("speedMps", "1000")]
    [InlineData("batteryPercentage", "-1")]
    [InlineData("batteryPercentage", "101")]
    [InlineData("timestamp", "not-a-date")]
    [InlineData("timestamp", "2026-11-12T08:00:00+07:00")]
    [InlineData("timestamp", "2026-11-12T08:00:00")]
    public async Task Invalid_fields_return_field_errors_without_storing_telemetry(string field, string value)
    {
        var actor = await NewActorAsync("ADMIN");
        var mission = await NewMissionAsync();
        var body = ValidSample();
        body[field] = field == "timestamp" ? value : field == "batteryPercentage"
            ? int.Parse(value) : decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        using var response = await PostAsync(mission, actor.Token, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
        await AssertNoSamplesAsync(mission);
    }

    [Theory]
    [InlineData("latitude")]
    [InlineData("longitude")]
    [InlineData("altitudeMeters")]
    [InlineData("speedMps")]
    [InlineData("batteryPercentage")]
    [InlineData("timestamp")]
    public async Task Missing_fields_are_not_silently_defaulted_to_zero(string field)
    {
        var actor = await NewActorAsync("ADMIN");
        var mission = await NewMissionAsync();
        var body = ValidSample();
        body.Remove(field);
        using var response = await PostAsync(mission, actor.Token, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
        await AssertNoSamplesAsync(mission);
    }

    [Fact]
    public async Task Future_timestamp_is_rejected()
    {
        var actor = await NewActorAsync("ADMIN");
        var mission = await NewMissionAsync();
        var body = ValidSample();
        body["timestamp"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O");
        using var response = await PostAsync(mission, actor.Token, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoSamplesAsync(mission);
    }

    [Theory]
    [InlineData(-90, -180, 0, 0, 0)]
    [InlineData(90, 180, 9999.99, 999.99, 100)]
    public async Task Boundary_values_and_zero_coordinates_are_valid(double latitude, double longitude, double altitude, double speed, int battery)
    {
        var actor = await NewActorAsync("ADMIN");
        var mission = await NewMissionAsync();
        var body = ValidSample();
        body["latitude"] = latitude; body["longitude"] = longitude;
        body["altitudeMeters"] = altitude; body["speedMps"] = speed; body["batteryPercentage"] = battery;
        using var response = await PostAsync(mission, actor.Token, body);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private Task AssertNoSamplesAsync(Guid mission) => factory.WithDbAsync(async db =>
        Assert.Equal(0, await db.FlightTelemetryLogs.CountAsync(x => x.MissionId == mission)));

    private async Task<(Guid Id, string Token)> NewActorAsync(string role)
    {
        var id = Guid.NewGuid();
        var email = $"telemetry-{id:N}@example.com";
        await factory.WithDbAsync(async db =>
        {
            db.Users.Add(new User
            {
                Id = id, Email = email, PhoneNumber = $"09{Random.Shared.Next(10000000, 99999999)}",
                FullName = "Telemetry actor", RoleId = (await db.Roles.SingleAsync(x => x.Code == role)).Id,
                PasswordHash = PasswordHash, Status = "ACTIVE"
            });
            await db.SaveChangesAsync();
        });
        using var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Passw0rd123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var data = await login.Content.ReadFromJsonAsync<JsonElement>();
        return (id, data.GetProperty("accessToken").GetString()!);
    }

    private async Task<Guid> NewMissionAsync(string status = "IN_AIR")
    {
        var id = Guid.Empty;
        await factory.WithDbAsync(async db => id = await TelemetryTestData.SeedMissionAsync(db, status));
        return id;
    }

    private async Task<HttpResponseMessage> PostAsync(Guid missionId, string? token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/flight-missions/{missionId}/telemetry");
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private static Dictionary<string, object?> ValidSample() => new()
    {
        ["latitude"] = 10.7769m, ["longitude"] = 106.7009m, ["altitudeMeters"] = 80m,
        ["speedMps"] = 11.5m, ["batteryPercentage"] = 71,
        ["timestamp"] = DateTimeOffset.UtcNow.AddSeconds(-10).ToString("O")
    };
}

internal static class TelemetryTestData
{
    public static async Task<Guid> SeedMissionAsync(AppDbContext db, string status = "IN_AIR")
    {
        var unique = Guid.NewGuid().ToString("N");
        var customer = new User
        {
            RoleId = (await db.Roles.FirstAsync(x => x.Code == "CUSTOMER")).Id,
            Email = $"telemetry-customer-{unique}@example.com", FullName = "Telemetry customer",
            PhoneNumber = unique[..15], Status = "ACTIVE"
        };
        var order = new DeliveryOrder
        {
            TrackingNumber = $"T-{unique}", Customer = customer, Status = "IN_TRANSIT", PickupSecureCode = "593284",
            Package = new Package { Sender = customer, PackageName = "Books", WeightGram = 450 },
            OriginStation = new LandingStation { StationCode = $"O-{unique[..8]}", Name = "Origin" },
            DestinationStation = new LandingStation { StationCode = $"D-{unique[..8]}", Name = "Destination" }
        };
        var mission = new FlightMission
        {
            Order = order, FlightStatus = status,
            Drone = new Drone
            {
                SerialNumber = $"TEL-{unique}", ModelName = "Test carrier", Status = "FLYING",
                MaxPayloadGram = 3000, MaxFlightRangeKm = 15, BatteryCapacityMah = 10000
            }
        };
        db.Add(mission);
        await db.SaveChangesAsync();
        return mission.Id;
    }
}
