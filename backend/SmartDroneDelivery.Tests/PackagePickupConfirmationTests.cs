using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>
/// LA-127: Package pickup confirmation unit & integration tests (docs/api/station-api.md section 4).
/// Tests directly against production StationOperationsController and StationOperationService.
/// </summary>
public class PackagePickupConfirmationTests : IClassFixture<AuthApiFactory>
{
    private const string Password = "Passw0rd123";
    private static int _counter;

    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public PackagePickupConfirmationTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Actor(Guid Id, string Access);

    private async Task<Actor> NewUserAsync(string roleCode)
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"ops-user-{n}@example.com";
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
                FullName = $"Ops User {n}",
                PhoneNumber = $"085{n:D7}",
                Status = "ACTIVE"
            });
            await db.SaveChangesAsync();
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Actor(userId, auth.GetProperty("accessToken").GetString()!);
    }

    private async Task<(LandingStation Origin, LandingStation Dest, DeliveryOrder Order, StationSlot DestSlot)> SetupTestScenarioAsync(
        Guid customerId, Guid originOpId, Guid destOpId, string initialStatus = "APPROVED")
    {
        var n = Interlocked.Increment(ref _counter);
        var origin = new LandingStation
        {
            Id = Guid.NewGuid(),
            StationCode = $"ORG-{n:D4}",
            Name = $"Origin {n}",
            Address = $"{n} Origin St",
            Latitude = 10.7m,
            Longitude = 106.7m,
            TotalSlots = 2,
            Status = "OPERATIONAL",
            OperatorId = originOpId
        };
        var originSlot = new StationSlot { Id = Guid.NewGuid(), StationId = origin.Id, SlotNumber = 1, Status = "EMPTY" };

        var dest = new LandingStation
        {
            Id = Guid.NewGuid(),
            StationCode = $"DST-{n:D4}",
            Name = $"Dest {n}",
            Address = $"{n} Dest St",
            Latitude = 10.8m,
            Longitude = 106.8m,
            TotalSlots = 2,
            Status = "OPERATIONAL",
            OperatorId = destOpId
        };
        var destSlot = new StationSlot { Id = Guid.NewGuid(), StationId = dest.Id, SlotNumber = 1, Status = initialStatus == "ARRIVED" ? "OCCUPIED" : "EMPTY" };

        var pkg = new Package
        {
            SenderId = customerId,
            PackageName = "Smartphone",
            WeightGram = 350,
            LengthCm = 15,
            WidthCm = 8,
            HeightCm = 3,
            IsFragile = true
        };

        var order = new DeliveryOrder
        {
            TrackingNumber = $"DD-OPS-{n:D4}",
            CustomerId = customerId,
            Package = pkg,
            OriginStationId = origin.Id,
            DestinationStationId = dest.Id,
            RecipientName = "Le Thi Huong",
            RecipientPhone = "0909123456",
            PickupSecureCode = "654321",
            Status = initialStatus
        };

        var drone = new Drone
        {
            SerialNumber = $"DRN-OPS-{n}",
            ModelName = "Carrier X",
            MaxPayloadGram = 2500,
            MaxFlightRangeKm = 30,
            BatteryCapacityMah = 18000
        };

        var mission = new FlightMission
        {
            Order = order,
            Drone = drone,
            StartSlot = originSlot,
            DestinationSlot = destSlot,
            FlightStatus = initialStatus == "ARRIVED" ? "COMPLETED" : "PLANNED"
        };

        await _factory.WithDbAsync(async db =>
        {
            db.LandingStations.AddRange(origin, dest);
            db.StationSlots.AddRange(originSlot, destSlot);
            db.Packages.Add(pkg);
            db.DeliveryOrders.Add(order);
            db.Drones.Add(drone);
            db.FlightMissions.Add(mission);
            await db.SaveChangesAsync();
        });

        return (origin, dest, order, destSlot);
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
    public async Task GetStationOrderInfo_masks_secure_code_and_recipient_phone_and_checks_station_role()
    {
        var customer = await NewUserAsync(AppRoles.Customer);
        var originOp = await NewUserAsync(AppRoles.StationOperator);
        var destOp = await NewUserAsync(AppRoles.StationOperator);
        var thirdOp = await NewUserAsync(AppRoles.StationOperator);
        var (origin, dest, order, _) = await SetupTestScenarioAsync(customer.Id, originOp.Id, destOp.Id);

        // Unassigned operator -> 403
        var forbiddenRes = await SendAsync(HttpMethod.Get,
            $"/api/landing-stations/{origin.Id}/operations/orders/{order.TrackingNumber}", thirdOp.Access);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRes.StatusCode);

        // Origin operator read -> role == "ORIGIN"
        var originRes = await SendAsync(HttpMethod.Get,
            $"/api/landing-stations/{origin.Id}/operations/orders/{order.TrackingNumber}", originOp.Access);
        Assert.Equal(HttpStatusCode.OK, originRes.StatusCode);
        var originJson = await JsonAsync(originRes);
        Assert.Equal("ORIGIN", originJson.GetProperty("role").GetString());
        Assert.False(originJson.TryGetProperty("pickupSecureCode", out _));
        Assert.False(originJson.TryGetProperty("recipientPhone", out _));

        // Destination operator read -> role == "DESTINATION"
        var destRes = await SendAsync(HttpMethod.Get,
            $"/api/landing-stations/{dest.Id}/operations/orders/{order.TrackingNumber}", destOp.Access);
        Assert.Equal(HttpStatusCode.OK, destRes.StatusCode);
        var destJson = await JsonAsync(destRes);
        Assert.Equal("DESTINATION", destJson.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Intake_validates_measures_updates_order_to_READY_FOR_TAKEOFF_and_slot_to_OCCUPIED()
    {
        var customer = await NewUserAsync(AppRoles.Customer);
        var originOp = await NewUserAsync(AppRoles.StationOperator);
        var destOp = await NewUserAsync(AppRoles.StationOperator);
        var (origin, _, order, _) = await SetupTestScenarioAsync(customer.Id, originOp.Id, destOp.Id);

        Guid emptySlotId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var s = await db.StationSlots.FirstAsync(x => x.StationId == origin.Id && x.Status == "EMPTY");
            emptySlotId = s.Id;
        });

        // Measurement over limit (> 3000g) -> 400
        var badWeightRes = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{origin.Id}/operations/intake", originOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                slotId = emptySlotId,
                measuredWeightGram = 3500m,
                measuredLengthCm = 20m,
                measuredWidthCm = 15m,
                measuredHeightCm = 10m,
                visualInspectionPassed = true
            });
        Assert.Equal(HttpStatusCode.BadRequest, badWeightRes.StatusCode);

        // Valid intake -> 200 OK
        var intakeRes = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{origin.Id}/operations/intake", originOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                slotId = emptySlotId,
                measuredWeightGram = 360m,
                measuredLengthCm = 16m,
                measuredWidthCm = 9m,
                measuredHeightCm = 4m,
                visualInspectionPassed = true
            });
        Assert.Equal(HttpStatusCode.OK, intakeRes.StatusCode);
        var intakeJson = await JsonAsync(intakeRes);
        Assert.Equal("READY_FOR_TAKEOFF", intakeJson.GetProperty("status").GetString());

        await _factory.WithDbAsync(async db =>
        {
            var updatedOrder = await db.DeliveryOrders.SingleAsync(o => o.Id == order.Id);
            Assert.Equal("READY_FOR_TAKEOFF", updatedOrder.Status);

            var updatedSlot = await db.StationSlots.SingleAsync(s => s.Id == emptySlotId);
            Assert.Equal("OCCUPIED", updatedSlot.Status);

            Assert.True(await db.DeliveryStatusLogs.AnyAsync(l => l.OrderId == order.Id && l.ToStatus == "READY_FOR_TAKEOFF"));
            Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == order.Id.ToString() && a.Action == "PACKAGE_INTAKE_CONFIRMATION"));
            Assert.True(await db.Notifications.AnyAsync(n => n.OrderId == order.Id && n.UserId == customer.Id));
        });

        // Repeating intake for already processed order -> 409
        var repeatRes = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{origin.Id}/operations/intake", originOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                slotId = emptySlotId,
                measuredWeightGram = 360m,
                measuredLengthCm = 16m,
                measuredWidthCm = 9m,
                measuredHeightCm = 4m,
                visualInspectionPassed = true
            });
        Assert.Equal(HttpStatusCode.Conflict, repeatRes.StatusCode);
    }

    [Fact]
    public async Task IntakeReject_sets_order_to_FAILED_and_records_cancelReason()
    {
        var customer = await NewUserAsync(AppRoles.Customer);
        var originOp = await NewUserAsync(AppRoles.StationOperator);
        var destOp = await NewUserAsync(AppRoles.StationOperator);
        var (origin, _, order, _) = await SetupTestScenarioAsync(customer.Id, originOp.Id, destOp.Id);

        var rejectRes = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{origin.Id}/operations/intake-reject", originOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                reason = "Seal damaged and liquid leaking."
            });
        Assert.Equal(HttpStatusCode.OK, rejectRes.StatusCode);
        var rejectJson = await JsonAsync(rejectRes);
        Assert.Equal("FAILED", rejectJson.GetProperty("status").GetString());

        await _factory.WithDbAsync(async db =>
        {
            var updated = await db.DeliveryOrders.SingleAsync(o => o.Id == order.Id);
            Assert.Equal("FAILED", updated.Status);
            Assert.Equal("Seal damaged and liquid leaking.", updated.CancelReason);
            Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == order.Id.ToString() && a.Action == "PACKAGE_INTAKE_REJECT"));
        });
    }

    [Fact]
    public async Task Pickup_enforces_3_attempt_lockout_and_releases_slot_on_success()
    {
        var customer = await NewUserAsync(AppRoles.Customer);
        var originOp = await NewUserAsync(AppRoles.StationOperator);
        var destOp = await NewUserAsync(AppRoles.StationOperator);
        var (_, dest, order, destSlot) = await SetupTestScenarioAsync(customer.Id, originOp.Id, destOp.Id, initialStatus: "ARRIVED");

        // Attempt 1: wrong code -> 400 Bad Request with attemptsRemaining = 2
        var wrong1 = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{dest.Id}/operations/pickup", destOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                pickupSecureCode = "000001"
            });
        Assert.Equal(HttpStatusCode.BadRequest, wrong1.StatusCode);
        var j1 = await JsonAsync(wrong1);
        Assert.Equal(2, j1.GetProperty("attemptsRemaining").GetInt32());

        // Advisory validate-code with wrong code -> counts as attempt 2 -> attemptsRemaining = 1
        var valRes = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{dest.Id}/operations/validate-code", destOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                pickupSecureCode = "000002"
            });
        Assert.Equal(HttpStatusCode.OK, valRes.StatusCode);
        var valJson = await JsonAsync(valRes);
        Assert.False(valJson.GetProperty("isValid").GetBoolean());
        Assert.Equal(1, valJson.GetProperty("attemptsRemaining").GetInt32());

        // Attempt 3: wrong code -> 423 Locked with Retry-After header and lockoutExpiresAt
        var wrong3 = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{dest.Id}/operations/pickup", destOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                pickupSecureCode = "000003"
            });
        Assert.Equal((HttpStatusCode)423, wrong3.StatusCode);
        Assert.True(wrong3.Headers.Contains("Retry-After"));
        var j3 = await JsonAsync(wrong3);
        Assert.True(j3.TryGetProperty("lockoutExpiresAt", out _));

        // Even with correct code, while locked -> 423 Locked
        var lockedTry = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{dest.Id}/operations/pickup", destOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                pickupSecureCode = order.PickupSecureCode
            });
        Assert.Equal((HttpStatusCode)423, lockedTry.StatusCode);

        // Reset lockout for testing successful pickup
        var (_, dest2, order2, destSlot2) = await SetupTestScenarioAsync(customer.Id, originOp.Id, destOp.Id, initialStatus: "ARRIVED");

        var successRes = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{dest2.Id}/operations/pickup", destOp.Access, new
            {
                trackingNumber = order2.TrackingNumber,
                pickupSecureCode = order2.PickupSecureCode
            });
        Assert.Equal(HttpStatusCode.OK, successRes.StatusCode);
        var successJson = await JsonAsync(successRes);
        Assert.Equal("DELIVERED", successJson.GetProperty("status").GetString());
        Assert.True(successJson.GetProperty("slotReleased").GetBoolean());

        await _factory.WithDbAsync(async db =>
        {
            var delOrder = await db.DeliveryOrders.SingleAsync(o => o.Id == order2.Id);
            Assert.Equal("DELIVERED", delOrder.Status);
            Assert.NotNull(delOrder.ActualDeliveryTime);

            var relSlot = await db.StationSlots.SingleAsync(s => s.Id == destSlot2.Id);
            Assert.Equal("EMPTY", relSlot.Status);

            var audit = await db.AuditLogs.SingleAsync(a => a.EntityId == order2.Id.ToString() && a.Action == "PACKAGE_DELIVERY_CONFIRMATION");
            // BR-LS-22: pickupSecureCode must never appear in logs or audit entries!
            Assert.DoesNotContain(order2.PickupSecureCode, audit.Action);
        });
    }
}
