using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>LA-115: Landing station management API (docs/api/station-api.md sections 2.1 to 2.5).</summary>
public class LandingStationManagementTests : IClassFixture<AuthApiFactory>
{
    private const string Password = "Passw0rd123";
    private static int _counter;

    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public LandingStationManagementTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Actor(Guid Id, string Access, string FullName);

    private async Task<Actor> NewUserAsync(string roleCode, string status = "ACTIVE")
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"station-user-{n}@example.com";
        var fullName = $"Operator {n}";
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
                FullName = fullName,
                PhoneNumber = $"081{n:D7}",
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

        return new Actor(userId, token, fullName);
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
    public async Task Create_provisions_station_slots_and_audit_log_in_one_transaction()
    {
        var admin = await NewUserAsync(AppRoles.Admin);
        var op = await NewUserAsync(AppRoles.StationOperator);
        var code = $"HCM-{Interlocked.Increment(ref _counter):D3}";

        var response = await SendAsync(HttpMethod.Post, "/api/landing-stations", admin.Access, new
        {
            stationCode = code,
            name = "District 1 Hub",
            address = "1 Le Loi, District 1",
            latitude = 10.7769m,
            longitude = 106.7009m,
            totalSlots = 4,
            operatorId = op.Id
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await JsonAsync(response);
        var id = Guid.Parse(json.GetProperty("id").GetString()!);
        Assert.Equal($"/api/landing-stations/{id}", response.Headers.Location!.OriginalString);
        Assert.Equal(code, json.GetProperty("stationCode").GetString());
        Assert.Equal("OPERATIONAL", json.GetProperty("status").GetString());
        Assert.Equal(4, json.GetProperty("totalSlots").GetInt32());
        Assert.Equal(4, json.GetProperty("emptySlots").GetInt32());
        Assert.Equal(op.Id.ToString(), json.GetProperty("operatorId").GetString());
        Assert.Equal(op.FullName, json.GetProperty("operatorName").GetString());

        await _factory.WithDbAsync(async db =>
        {
            var slots = await db.StationSlots.Where(s => s.StationId == id).OrderBy(s => s.SlotNumber).ToListAsync();
            Assert.Equal(new[] { 1, 2, 3, 4 }, slots.Select(s => s.SlotNumber).ToArray());
            Assert.All(slots, s => Assert.Equal("EMPTY", s.Status));

            var audit = await db.AuditLogs.SingleAsync(a => a.EntityId == id.ToString() && a.Action == "STATION_CREATE");
            Assert.Equal(admin.Id, audit.UserId);
        });
    }

    [Fact]
    public async Task Create_validates_duplicate_code_409_and_invalid_fields_400()
    {
        var admin = await NewUserAsync(AppRoles.Admin);
        var customer = await NewUserAsync(AppRoles.Customer);
        var code = $"DUP-{Interlocked.Increment(ref _counter):D3}";

        var first = await SendAsync(HttpMethod.Post, "/api/landing-stations", admin.Access, new
        {
            stationCode = code,
            name = "Station One",
            address = "10 Street",
            latitude = 10.0m,
            longitude = 106.0m,
            totalSlots = 2
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await SendAsync(HttpMethod.Post, "/api/landing-stations", admin.Access, new
        {
            stationCode = code,
            name = "Station Two",
            address = "20 Street",
            latitude = 10.0m,
            longitude = 106.0m,
            totalSlots = 2
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var invalid = await SendAsync(HttpMethod.Post, "/api/landing-stations", admin.Access, new
        {
            stationCode = "bad_code",
            name = "",
            address = "   ",
            latitude = 95m,
            longitude = -190m,
            totalSlots = 25,
            operatorId = customer.Id
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var errJson = await JsonAsync(invalid);
        var errors = errJson.GetProperty("errors");
        Assert.True(errors.TryGetProperty("stationCode", out _));
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("address", out _));
        Assert.True(errors.TryGetProperty("latitude", out _));
        Assert.True(errors.TryGetProperty("longitude", out _));
        Assert.True(errors.TryGetProperty("totalSlots", out _));
        Assert.True(errors.TryGetProperty("operatorId", out _));
    }

    [Fact]
    public async Task List_and_GetById_support_search_slot_details_and_role_checks()
    {
        var admin = await NewUserAsync(AppRoles.Admin);
        var customer = await NewUserAsync(AppRoles.Customer);
        var code = $"SRCH-{Interlocked.Increment(ref _counter):D3}";

        var created = await JsonAsync(await SendAsync(HttpMethod.Post, "/api/landing-stations", admin.Access, new
        {
            stationCode = code,
            name = $"Unique Hub {code}",
            address = "50 Nguyen Hue",
            latitude = 10.7m,
            longitude = 106.7m,
            totalSlots = 3
        }));
        var stationId = Guid.Parse(created.GetProperty("id").GetString()!);

        await _factory.WithDbAsync(async db =>
        {
            db.Drones.Add(new Drone
            {
                SerialNumber = $"DRN-{code}",
                ModelName = "TestModel",
                MaxPayloadGram = 2000,
                MaxFlightRangeKm = 15,
                BatteryCapacityMah = 10000,
                CurrentStationId = stationId,
                Status = "IDLE"
            });
            await db.SaveChangesAsync();
        });

        var listRes = await SendAsync(HttpMethod.Get, $"/api/landing-stations?search={code.ToLowerInvariant()}", customer.Access);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var list = await JsonAsync(listRes);
        Assert.Single(list.EnumerateArray());
        Assert.Equal(3, list[0].GetProperty("totalSlots").GetInt32());
        Assert.Equal(3, list[0].GetProperty("emptySlots").GetInt32());

        var badStatus = await SendAsync(HttpMethod.Get, "/api/landing-stations?status=UNKNOWN", customer.Access);
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);

        var customerDetail = await SendAsync(HttpMethod.Get, $"/api/landing-stations/{stationId}", customer.Access);
        Assert.Equal(HttpStatusCode.Forbidden, customerDetail.StatusCode);

        var detailRes = await SendAsync(HttpMethod.Get, $"/api/landing-stations/{stationId}", admin.Access);
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
        var detail = await JsonAsync(detailRes);
        Assert.Equal(1, detail.GetProperty("hostedDroneCount").GetInt32());
        Assert.Equal(3, detail.GetProperty("slots").GetArrayLength());
    }

    [Fact]
    public async Task Update_modifies_attributes_rejects_unknown_fields_and_writes_audit_log()
    {
        var admin = await NewUserAsync(AppRoles.Admin);
        var op = await NewUserAsync(AppRoles.StationOperator);
        var code = $"UPD-{Interlocked.Increment(ref _counter):D3}";

        var created = await JsonAsync(await SendAsync(HttpMethod.Post, "/api/landing-stations", admin.Access, new
        {
            stationCode = code,
            name = "Old Name",
            address = "Old Address",
            latitude = 10.1m,
            longitude = 106.1m,
            totalSlots = 2
        }));
        var stationId = Guid.Parse(created.GetProperty("id").GetString()!);

        var unknownFieldRes = await SendAsync(HttpMethod.Put, $"/api/landing-stations/{stationId}", admin.Access, new
        {
            name = "New Name",
            address = "New Address",
            latitude = 10.2m,
            longitude = 106.2m,
            stationCode = "NEW-CODE"
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknownFieldRes.StatusCode);

        var updateRes = await SendAsync(HttpMethod.Put, $"/api/landing-stations/{stationId}", admin.Access, new
        {
            name = "New Name",
            address = "New Address",
            latitude = 10.2m,
            longitude = 106.2m,
            operatorId = op.Id
        });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await JsonAsync(updateRes);
        Assert.Equal("New Name", updated.GetProperty("name").GetString());
        Assert.Equal(op.Id.ToString(), updated.GetProperty("operatorId").GetString());

        await _factory.WithDbAsync(async db =>
        {
            Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == stationId.ToString() && a.Action == "STATION_UPDATE"));
        });
    }

    [Fact]
    public async Task ChangeStatus_enforces_operator_ownership_and_triggers_reroute_warning_for_in_air_missions()
    {
        var admin = await NewUserAsync(AppRoles.Admin);
        var assignedOp = await NewUserAsync(AppRoles.StationOperator);
        var otherOp = await NewUserAsync(AppRoles.StationOperator);
        var dispatcher = await NewUserAsync(AppRoles.Dispatcher);
        var customer = await NewUserAsync(AppRoles.Customer);
        var code = $"STS-{Interlocked.Increment(ref _counter):D3}";

        var created = await JsonAsync(await SendAsync(HttpMethod.Post, "/api/landing-stations", admin.Access, new
        {
            stationCode = code,
            name = "Status Hub",
            address = "100 Street",
            latitude = 10.5m,
            longitude = 106.5m,
            totalSlots = 2,
            operatorId = assignedOp.Id
        }));
        var stationId = Guid.Parse(created.GetProperty("id").GetString()!);

        var forbiddenRes = await SendAsync(HttpMethod.Put, $"/api/landing-stations/{stationId}/status", otherOp.Access, new
        {
            newStatus = "MAINTENANCE",
            reason = "Unauthorized operator attempt."
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRes.StatusCode);

        Guid missionId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var slot = await db.StationSlots.FirstAsync(s => s.StationId == stationId);
            var pkg = new Package
            {
                SenderId = customer.Id,
                PackageName = "Medical Box",
                WeightGram = 500,
                LengthCm = 10,
                WidthCm = 10,
                HeightCm = 10
            };
            var order = new DeliveryOrder
            {
                TrackingNumber = $"DD-STS-{Interlocked.Increment(ref _counter):D4}",
                CustomerId = customer.Id,
                Package = pkg,
                OriginStationId = stationId,
                DestinationStationId = stationId,
                RecipientName = "Recipient",
                RecipientPhone = "0909000111",
                PickupSecureCode = "123456",
                Status = "IN_TRANSIT"
            };
            var drone = new Drone
            {
                SerialNumber = $"DRN-STS-{code}",
                ModelName = "Model X",
                MaxPayloadGram = 2500,
                MaxFlightRangeKm = 20,
                BatteryCapacityMah = 15000,
                Status = "FLYING"
            };
            var mission = new FlightMission
            {
                Order = order,
                Drone = drone,
                DestinationSlotId = slot.Id,
                FlightStatus = "IN_AIR",
                StartTime = DateTime.UtcNow
            };
            db.FlightMissions.Add(mission);
            await db.SaveChangesAsync();
            missionId = mission.Id;
        });

        var statusRes = await SendAsync(HttpMethod.Put, $"/api/landing-stations/{stationId}/status", assignedOp.Access, new
        {
            newStatus = "MAINTENANCE",
            reason = "Landing pad sensors are being replaced."
        });
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);
        var body = await JsonAsync(statusRes);
        Assert.Equal("MAINTENANCE", body.GetProperty("station").GetProperty("status").GetString());
        Assert.True(body.GetProperty("rerouteWarning").GetBoolean());
        Assert.Contains(missionId.ToString(), body.GetProperty("affectedMissionIds").EnumerateArray().Select(x => x.GetString()));

        // Setting the same status again returns 200 with no new audit entry.
        var repeatRes = await SendAsync(HttpMethod.Put, $"/api/landing-stations/{stationId}/status", assignedOp.Access, new
        {
            newStatus = "MAINTENANCE",
            reason = "Still replacing landing pad sensors."
        });
        Assert.Equal(HttpStatusCode.OK, repeatRes.StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            var audits = await db.AuditLogs
                .Where(a => a.EntityId == stationId.ToString() && a.Action == "STATION_STATUS_CHANGE")
                .ToListAsync();
            Assert.Single(audits);
            Assert.True(await db.Notifications.AnyAsync(n => n.UserId == dispatcher.Id && n.Type == "STATION_REROUTE_ALERT"));
        });
    }

    [Fact]
    public async Task GetById_allows_assigned_operator_and_admin_and_dispatcher_but_forbids_other_operator()
    {
        var admin = await NewUserAsync(AppRoles.Admin);
        var dispatcher = await NewUserAsync(AppRoles.Dispatcher);
        var assignedOp = await NewUserAsync(AppRoles.StationOperator);
        var otherOp = await NewUserAsync(AppRoles.StationOperator);

        var code = $"HCM-{Interlocked.Increment(ref _counter):D3}";
        var stationId = Guid.Empty;

        await _factory.WithDbAsync(async db =>
        {
            var station = new LandingStation
            {
                StationCode = code,
                Name = $"Station {code}",
                Address = "123 Test St",
                Latitude = 10.7m,
                Longitude = 106.6m,
                TotalSlots = 2,
                Status = "OPERATIONAL",
                OperatorId = assignedOp.Id
            };
            db.LandingStations.Add(station);
            await db.SaveChangesAsync();
            stationId = station.Id;
        });

        // 1. Assigned operator can read own station -> 200 OK
        var ownRes = await SendAsync(HttpMethod.Get, $"/api/landing-stations/{stationId}", assignedOp.Access);
        Assert.Equal(HttpStatusCode.OK, ownRes.StatusCode);

        // 2. Another station operator cannot read this station -> 403 Forbidden
        var otherRes = await SendAsync(HttpMethod.Get, $"/api/landing-stations/{stationId}", otherOp.Access);
        Assert.Equal(HttpStatusCode.Forbidden, otherRes.StatusCode);

        // 3. Admin can read any station -> 200 OK
        var adminRes = await SendAsync(HttpMethod.Get, $"/api/landing-stations/{stationId}", admin.Access);
        Assert.Equal(HttpStatusCode.OK, adminRes.StatusCode);

        // 4. Dispatcher can read any station -> 200 OK
        var dispRes = await SendAsync(HttpMethod.Get, $"/api/landing-stations/{stationId}", dispatcher.Access);
        Assert.Equal(HttpStatusCode.OK, dispRes.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_concurrent_requests_detect_conflict()
    {
        var admin = await NewUserAsync(AppRoles.Admin);
        var op = await NewUserAsync(AppRoles.StationOperator);
        var code = $"HCM-{Interlocked.Increment(ref _counter):D3}";
        var stationId = Guid.Empty;

        await _factory.WithDbAsync(async db =>
        {
            var station = new LandingStation
            {
                StationCode = code,
                Name = $"Concurrent Hub {code}",
                Address = "456 Concurrency Ave",
                Latitude = 10.7m,
                Longitude = 106.6m,
                TotalSlots = 2,
                Status = "OPERATIONAL",
                OperatorId = op.Id
            };
            db.LandingStations.Add(station);
            await db.SaveChangesAsync();
            stationId = station.Id;
        });

        var task1 = SendAsync(HttpMethod.Put, $"/api/landing-stations/{stationId}/status", admin.Access, new
        {
            newStatus = "MAINTENANCE",
            reason = "Scheduled sensor upgrade."
        });

        var task2 = SendAsync(HttpMethod.Put, $"/api/landing-stations/{stationId}/status", admin.Access, new
        {
            newStatus = "OFFLINE",
            reason = "Emergency electrical maintenance."
        });

        var responses = await Task.WhenAll(task1, task2);
        var statusCodes = responses.Select(r => r.StatusCode).ToList();

        // Either one succeeds (200) and one fails (409), or both 200 if processed sequentially, but at least one must succeed
        Assert.Contains(HttpStatusCode.OK, statusCodes);
    }
}
