using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>LA-118: Station slot management API (docs/api/station-api.md sections 3.1 to 3.5).</summary>
public class StationSlotManagementTests : IClassFixture<AuthApiFactory>
{
    private const string Password = "Passw0rd123";
    private static int _counter;

    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public StationSlotManagementTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Actor(Guid Id, string Access);

    private async Task<Actor> NewUserAsync(string roleCode)
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"slot-user-{n}@example.com";
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
                FullName = $"Slot User {n}",
                PhoneNumber = $"082{n:D7}",
                Status = "ACTIVE"
            });
            await db.SaveChangesAsync();
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Actor(userId, auth.GetProperty("accessToken").GetString()!);
    }

    private async Task<(LandingStation Station, List<StationSlot> Slots)> SeedStationAsync(int slotCount = 2, Guid? operatorId = null)
    {
        var n = Interlocked.Increment(ref _counter);
        var station = new LandingStation
        {
            Id = Guid.NewGuid(),
            StationCode = $"SL-{n:D4}",
            Name = $"Slot Station {n}",
            Address = $"{n} Slot Rd",
            Latitude = 10.7m,
            Longitude = 106.7m,
            TotalSlots = slotCount,
            Status = "OPERATIONAL",
            OperatorId = operatorId
        };
        var slots = Enumerable.Range(1, slotCount).Select(i => new StationSlot
        {
            Id = Guid.NewGuid(),
            StationId = station.Id,
            SlotNumber = i,
            Status = "EMPTY"
        }).ToList();

        await _factory.WithDbAsync(async db =>
        {
            db.LandingStations.Add(station);
            db.StationSlots.AddRange(slots);
            await db.SaveChangesAsync();
        });

        return (station, slots);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? bearer, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task AddSlot_and_RemoveSlot_update_totalSlots_and_enforce_constraints()
    {
        var admin = await NewUserAsync(AppRoles.Admin);
        var (station, initialSlots) = await SeedStationAsync(slotCount: 1);

        // Cannot remove the last slot of a station -> 409
        var delLast = await SendAsync(HttpMethod.Delete, $"/api/station-slots/{initialSlots[0].Id}", admin.Access);
        Assert.Equal(HttpStatusCode.Conflict, delLast.StatusCode);

        // Add slot without specifying slotNumber -> auto max + 1 = 2
        var addRes = await SendAsync(HttpMethod.Post, $"/api/landing-stations/{station.Id}/slots", admin.Access, new { });
        Assert.Equal(HttpStatusCode.Created, addRes.StatusCode);
        var added = await JsonAsync(addRes);
        var slot2Id = Guid.Parse(added.GetProperty("id").GetString()!);
        Assert.Equal(2, added.GetProperty("slotNumber").GetInt32());
        Assert.Equal("EMPTY", added.GetProperty("status").GetString());

        // Duplicate slotNumber -> 409
        var dupRes = await SendAsync(HttpMethod.Post, $"/api/landing-stations/{station.Id}/slots", admin.Access, new { slotNumber = 2 });
        Assert.Equal(HttpStatusCode.Conflict, dupRes.StatusCode);

        // Invalid slotNumber < 1 -> 400
        var badRes = await SendAsync(HttpMethod.Post, $"/api/landing-stations/{station.Id}/slots", admin.Access, new { slotNumber = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);

        // Delete newly added slot -> 204
        var delRes = await SendAsync(HttpMethod.Delete, $"/api/station-slots/{slot2Id}", admin.Access);
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            var st = await db.LandingStations.SingleAsync(s => s.Id == station.Id);
            Assert.Equal(1, st.TotalSlots);
        });
    }

    [Fact]
    public async Task RemoveSlot_rejects_occupied_reserved_or_mission_linked_slots_with_409()
    {
        var admin = await NewUserAsync(AppRoles.Admin);
        var customer = await NewUserAsync(AppRoles.Customer);
        var (station, slots) = await SeedStationAsync(slotCount: 3);

        await _factory.WithDbAsync(async db =>
        {
            var s1 = await db.StationSlots.SingleAsync(s => s.Id == slots[0].Id);
            s1.Status = "OCCUPIED";

            var pkg = new Package { SenderId = customer.Id, PackageName = "P", WeightGram = 100, LengthCm = 5, WidthCm = 5, HeightCm = 5 };
            var order = new DeliveryOrder
            {
                TrackingNumber = $"DD-SL-{Interlocked.Increment(ref _counter):D4}",
                CustomerId = customer.Id,
                Package = pkg,
                OriginStationId = station.Id,
                DestinationStationId = station.Id,
                RecipientName = "R",
                RecipientPhone = "0901234567",
                PickupSecureCode = "111222"
            };
            var drone = new Drone { SerialNumber = $"DRN-SL-{station.StationCode}", ModelName = "M", MaxPayloadGram = 1000, MaxFlightRangeKm = 10, BatteryCapacityMah = 5000 };
            db.FlightMissions.Add(new FlightMission
            {
                Order = order,
                Drone = drone,
                StartSlotId = slots[1].Id,
                FlightStatus = "PLANNED"
            });
            await db.SaveChangesAsync();
        });

        var delOccupied = await SendAsync(HttpMethod.Delete, $"/api/station-slots/{slots[0].Id}", admin.Access);
        Assert.Equal(HttpStatusCode.Conflict, delOccupied.StatusCode);

        var delLinked = await SendAsync(HttpMethod.Delete, $"/api/station-slots/{slots[1].Id}", admin.Access);
        Assert.Equal(HttpStatusCode.Conflict, delLinked.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_isolates_and_restores_slot_and_validates_transitions_and_ownership()
    {
        var admin = await NewUserAsync(AppRoles.Admin);
        var assignedOp = await NewUserAsync(AppRoles.StationOperator);
        var otherOp = await NewUserAsync(AppRoles.StationOperator);
        var (station, slots) = await SeedStationAsync(slotCount: 2, operatorId: assignedOp.Id);

        // Unassigned operator -> 403
        var forbidden = await SendAsync(HttpMethod.Put, $"/api/station-slots/{slots[0].Id}/status", otherOp.Access, new
        {
            targetStatus = "ERROR",
            reason = "Charging pin is damaged."
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // EMPTY -> EMPTY -> 400
        var emptyToEmpty = await SendAsync(HttpMethod.Put, $"/api/station-slots/{slots[0].Id}/status", assignedOp.Access, new
        {
            targetStatus = "EMPTY",
            reason = "Not in maintenance."
        });
        Assert.Equal(HttpStatusCode.BadRequest, emptyToEmpty.StatusCode);

        // System-only status OCCUPIED -> 400
        var toOccupied = await SendAsync(HttpMethod.Put, $"/api/station-slots/{slots[0].Id}/status", assignedOp.Access, new
        {
            targetStatus = "OCCUPIED",
            reason = "Cannot set occupied manually."
        });
        Assert.Equal(HttpStatusCode.BadRequest, toOccupied.StatusCode);

        // EMPTY -> ERROR -> 200
        var toError = await SendAsync(HttpMethod.Put, $"/api/station-slots/{slots[0].Id}/status", assignedOp.Access, new
        {
            targetStatus = "ERROR",
            reason = "Charging pin is damaged."
        });
        Assert.Equal(HttpStatusCode.OK, toError.StatusCode);
        Assert.Equal("ERROR", (await JsonAsync(toError)).GetProperty("status").GetString());

        // ERROR -> EMPTY -> 200
        var toEmpty = await SendAsync(HttpMethod.Put, $"/api/station-slots/{slots[0].Id}/status", admin.Access, new
        {
            targetStatus = "EMPTY",
            reason = "Charging pin replaced and verified."
        });
        Assert.Equal(HttpStatusCode.OK, toEmpty.StatusCode);
        Assert.Equal("EMPTY", (await JsonAsync(toEmpty)).GetProperty("status").GetString());

        // OCCUPIED -> ERROR -> 409
        await _factory.WithDbAsync(async db =>
        {
            var s2 = await db.StationSlots.SingleAsync(s => s.Id == slots[1].Id);
            s2.Status = "OCCUPIED";
            await db.SaveChangesAsync();
        });
        var occToError = await SendAsync(HttpMethod.Put, $"/api/station-slots/{slots[1].Id}/status", assignedOp.Access, new
        {
            targetStatus = "ERROR",
            reason = "Pad sensor fault."
        });
        Assert.Equal(HttpStatusCode.Conflict, occToError.StatusCode);
    }
}
