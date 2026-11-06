using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>LA-121: Station availability monitoring (docs/api/station-api.md section 2.6).</summary>
public class StationAvailabilityTests : IClassFixture<AuthApiFactory>
{
    private const string Password = "Passw0rd123";
    private static int _counter;

    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public StationAvailabilityTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Actor(Guid Id, string Access);

    private async Task<Actor> NewUserAsync(string roleCode = AppRoles.Customer, string status = "ACTIVE")
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"avail-user-{n}@example.com";
        var userId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.SingleAsync(r => r.Code == roleCode);
            db.Users.Add(new User
            {
                Id = userId,
                RoleId = role.Id,
                Email = email,
                PasswordHash = new PasswordHasher().Hash(Password),
                FullName = $"User {n}",
                PhoneNumber = $"083{n:D7}",
                Status = "ACTIVE"
            });
            await db.SaveChangesAsync();
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = auth.GetProperty("accessToken").GetString()!;

        if (status != "ACTIVE")
        {
            await _factory.WithDbAsync(async db =>
            {
                var u = await db.Users.SingleAsync(x => x.Id == userId);
                u.Status = status;
                await db.SaveChangesAsync();
            });
        }

        return new Actor(userId, token);
    }

    private async Task<LandingStation> CreateStationWithSlotsAsync(string stationStatus, params string[] slotStatuses)
    {
        var n = Interlocked.Increment(ref _counter);
        var station = new LandingStation
        {
            Id = Guid.NewGuid(),
            StationCode = $"AV-{n:D4}",
            Name = $"Station {n}",
            Address = $"{n} Avail Way",
            Latitude = 10.7m,
            Longitude = 106.7m,
            TotalSlots = slotStatuses.Length,
            Status = stationStatus
        };

        var slots = slotStatuses.Select((st, idx) => new StationSlot
        {
            Id = Guid.NewGuid(),
            StationId = station.Id,
            SlotNumber = idx + 1,
            Status = st
        }).ToList();

        await _factory.WithDbAsync(async db =>
        {
            db.LandingStations.Add(station);
            db.StationSlots.AddRange(slots);
            await db.SaveChangesAsync();
        });

        return station;
    }

    private async Task<HttpResponseMessage> SendGetAsync(string url, string? bearer)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task GetAvailability_returns_accurate_live_counts_and_availability_flags()
    {
        var user = await NewUserAsync();
        var station = await CreateStationWithSlotsAsync("OPERATIONAL", "EMPTY", "EMPTY", "OCCUPIED", "RESERVED", "ERROR");

        var response = await SendGetAsync($"/api/landing-stations/{station.Id}/availability", user.Access);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(station.Id.ToString(), json.GetProperty("stationId").GetString());
        Assert.Equal("OPERATIONAL", json.GetProperty("stationStatus").GetString());
        Assert.Equal(5, json.GetProperty("totalSlots").GetInt32());
        Assert.Equal(2, json.GetProperty("emptySlots").GetInt32());
        Assert.Equal(1, json.GetProperty("occupiedSlots").GetInt32());
        Assert.Equal(1, json.GetProperty("reservedSlots").GetInt32());
        Assert.Equal(1, json.GetProperty("errorSlots").GetInt32());
        Assert.True(json.GetProperty("isAvailableForArrival").GetBoolean());
        Assert.True(json.GetProperty("isAvailableForDeparture").GetBoolean());
    }

    [Fact]
    public async Task GetAvailability_is_false_when_maintenance_or_no_empty_slots()
    {
        var user = await NewUserAsync();
        var maintStation = await CreateStationWithSlotsAsync("MAINTENANCE", "EMPTY", "EMPTY");
        var fullStation = await CreateStationWithSlotsAsync("OPERATIONAL", "OCCUPIED", "RESERVED");

        var maintRes = await SendGetAsync($"/api/landing-stations/{maintStation.Id}/availability", user.Access);
        Assert.Equal(HttpStatusCode.OK, maintRes.StatusCode);
        var maintJson = await maintRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(maintJson.GetProperty("isAvailableForArrival").GetBoolean());
        Assert.False(maintJson.GetProperty("isAvailableForDeparture").GetBoolean());

        var fullRes = await SendGetAsync($"/api/landing-stations/{fullStation.Id}/availability", user.Access);
        Assert.Equal(HttpStatusCode.OK, fullRes.StatusCode);
        var fullJson = await fullRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(fullJson.GetProperty("isAvailableForArrival").GetBoolean());
        Assert.False(fullJson.GetProperty("isAvailableForDeparture").GetBoolean());
    }

    [Fact]
    public async Task GetAvailability_returns_404_when_station_does_not_exist()
    {
        var user = await NewUserAsync();
        var response = await SendGetAsync($"/api/landing-stations/{Guid.NewGuid()}/availability", user.Access);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
