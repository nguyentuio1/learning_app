// LA-172: End-to-end full system integration tests across delivery workflow, tracking, ETA, confirmation, external API, and analytics
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class FullSystemIntegrationTests(SimulatorApiFactory factory) : IClassFixture<SimulatorApiFactory>
{
    private static int _seq;

    private async Task<(Guid Id, string Token)> CreateActorAsync(string roleCode)
    {
        var n = Interlocked.Increment(ref _seq);
        var id = Guid.NewGuid();
        var token = "";
        await factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.SingleAsync(r => r.Code == roleCode);
            var user = new User
            {
                Id = id,
                Email = $"e2e-{roleCode.ToLowerInvariant()}-{n}-{Guid.NewGuid():N}@example.com",
                FullName = $"E2E {roleCode} {n}",
                PasswordHash = "unused",
                PhoneNumber = $"09{n:D8}",
                RoleId = role.Id,
                Role = role,
                Status = "ACTIVE"
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            using var scope = factory.Services.CreateScope();
            token = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, DateTime.UtcNow);
        });
        return (id, token);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? bearer, object? body = null)
    {
        using var client = factory.CreateClient();
        using var req = new HttpRequestMessage(method, path);
        if (bearer is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (body is not null)
            req.Content = JsonContent.Create(body);
        return await client.SendAsync(req);
    }

    [Fact]
    public async Task EndToEnd_order_creation_approval_schedule_intake_flight_tracking_eta_confirmation_and_analytics_flow()
    {
        factory.Clock.Now = DateTimeOffset.UtcNow;
        var customer = await CreateActorAsync(AppRoles.Customer);
        var dispatcher = await CreateActorAsync(AppRoles.Dispatcher);
        var originOperator = await CreateActorAsync(AppRoles.StationOperator);
        var destOperator = await CreateActorAsync(AppRoles.StationOperator);
        var manager = await CreateActorAsync(AppRoles.Manager);

        var n = Interlocked.Increment(ref _seq);
        var originStation = new LandingStation
        {
            Id = Guid.NewGuid(),
            StationCode = $"E2E-OR-{n:D4}",
            Name = $"Origin Hub {n}",
            Address = "1 Origin Way",
            Latitude = 0m,
            Longitude = 0m,
            TotalSlots = 2,
            Status = "OPERATIONAL",
            OperatorId = originOperator.Id
        };
        var destStation = new LandingStation
        {
            Id = Guid.NewGuid(),
            StationCode = $"E2E-DE-{n:D4}",
            Name = $"Dest Hub {n}",
            Address = "2 Dest Way",
            Latitude = 0m,
            Longitude = 0.01m,
            TotalSlots = 2,
            Status = "OPERATIONAL",
            OperatorId = destOperator.Id
        };
        var originSlot = new StationSlot { Id = Guid.NewGuid(), StationId = originStation.Id, SlotNumber = 1, Status = "EMPTY" };
        var destSlot = new StationSlot { Id = Guid.NewGuid(), StationId = destStation.Id, SlotNumber = 1, Status = "EMPTY" };
        var drone = new Drone
        {
            Id = Guid.NewGuid(),
            SerialNumber = $"E2E-DRN-{n:D4}",
            ModelName = "Falcon E2E",
            MaxPayloadGram = 2500,
            MaxFlightRangeKm = 30m,
            BatteryCapacityMah = 18000,
            CurrentStationId = originStation.Id,
            Status = "IDLE"
        };
        var package = new Package
        {
            Id = Guid.NewGuid(),
            SenderId = customer.Id,
            PackageName = "Medical Kit",
            WeightGram = 450m,
            LengthCm = 20m,
            WidthCm = 15m,
            HeightCm = 10m
        };

        await factory.WithDbAsync(async db =>
        {
            db.LandingStations.AddRange(originStation, destStation);
            db.StationSlots.AddRange(originSlot, destSlot);
            db.Drones.Add(drone);
            db.Packages.Add(package);
            await db.SaveChangesAsync();
        });

        // 1. Customer creates delivery order -> PENDING
        Guid orderId;
        string trackingNumber;
        string pickupSecureCode;
        using (var createRes = await SendAsync(HttpMethod.Post, "/api/delivery-orders", customer.Token, new
        {
            packageId = package.Id,
            originStationId = originStation.Id,
            destinationStationId = destStation.Id,
            recipientName = "Recipient E2E",
            recipientPhone = "0912345678"
        }))
        {
            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
            var createJson = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            orderId = createJson.GetProperty("id").GetGuid();
            trackingNumber = createJson.GetProperty("trackingNumber").GetString()!;
            pickupSecureCode = createJson.GetProperty("pickupSecureCode").GetString()!;
            Assert.Equal("PENDING", createJson.GetProperty("status").GetString());
        }

        // 2. Dispatcher approves order -> APPROVED
        using (var approveRes = await SendAsync(HttpMethod.Post, $"/api/delivery-orders/{orderId}/approve", dispatcher.Token))
        {
            Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);
            var approveJson = await approveRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("APPROVED", approveJson.GetProperty("status").GetString());
        }

        // 3. Dispatcher schedules departure and assigns drone -> PLANNED mission, RESERVED destination slot
        var departure = factory.Clock.Now.AddMinutes(30);
        using (var scheduleRes = await SendAsync(HttpMethod.Put, $"/api/delivery-orders/{orderId}/schedule", dispatcher.Token, new
        {
            scheduledDepartureTime = departure.UtcDateTime
        }))
        {
            Assert.Equal(HttpStatusCode.OK, scheduleRes.StatusCode);
        }

        using (var assignRes = await SendAsync(HttpMethod.Post, $"/api/delivery-orders/{orderId}/flight-mission", dispatcher.Token, new
        {
            droneId = drone.Id
        }))
        {
            Assert.Equal(HttpStatusCode.Created, assignRes.StatusCode);
        }

        // 4. Origin Station Operator performs intake -> READY_FOR_TAKEOFF, OCCUPIED origin slot
        using (var intakeRes = await SendAsync(HttpMethod.Post, $"/api/landing-stations/{originStation.Id}/operations/intake", originOperator.Token, new
        {
            trackingNumber,
            slotId = originSlot.Id,
            measuredWeightGram = 450,
            measuredLengthCm = 20,
            measuredWidthCm = 15,
            measuredHeightCm = 10,
            visualInspectionPassed = true
        }))
        {
            Assert.Equal(HttpStatusCode.OK, intakeRes.StatusCode);
            var intakeJson = await intakeRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("READY_FOR_TAKEOFF", intakeJson.GetProperty("status").GetString());
        }

        // 5. Flight Simulator takes off at departure time -> IN_TRANSIT, live tracking + ETA available
        factory.Clock.Now = departure.AddSeconds(1);
        await SimulatorData.TickAsync(factory.Services);

        using (var trackingRes = await SendAsync(HttpMethod.Get, $"/api/delivery-orders/{orderId}/tracking", customer.Token))
        {
            Assert.Equal(HttpStatusCode.OK, trackingRes.StatusCode);
            var trackingJson = await trackingRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("IN_TRANSIT", trackingJson.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Object, trackingJson.GetProperty("lastPosition").ValueKind);
        }

        using (var etaRes = await SendAsync(HttpMethod.Get, $"/api/delivery-orders/{orderId}/eta", dispatcher.Token))
        {
            Assert.Equal(HttpStatusCode.OK, etaRes.StatusCode);
            Assert.True(etaRes.Headers.CacheControl?.NoStore);
            var etaJson = await etaRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("IN_TRANSIT", etaJson.GetProperty("status").GetString());
            Assert.NotEqual(JsonValueKind.Null, etaJson.GetProperty("estimatedDeliveryTime").ValueKind);
        }

        // 6. Flight Simulator lands at destination -> ARRIVED (distinct from DELIVERED), destination slot OCCUPIED
        factory.Clock.Now = departure.AddMinutes(10);
        await SimulatorData.TickAsync(factory.Services);

        await factory.WithDbAsync(async db =>
        {
            var arrivedOrder = await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == orderId);
            Assert.Equal("ARRIVED", arrivedOrder.Status);
            Assert.Null(arrivedOrder.ActualDeliveryTime);
            var dbDestSlot = await db.StationSlots.AsNoTracking().SingleAsync(s => s.Id == destSlot.Id);
            Assert.Equal("OCCUPIED", dbDestSlot.Status);
        });

        // 7. Customer confirms delivery with PickupSecureCode -> DELIVERED, destination slot EMPTY, reflected in Analytics
        using (var confirmRes = await SendAsync(HttpMethod.Post, $"/api/delivery-orders/{orderId}/confirm-delivery", customer.Token, new
        {
            pickupSecureCode
        }))
        {
            Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
            var confirmJson = await confirmRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("DELIVERED", confirmJson.GetProperty("status").GetString());
        }

        await factory.WithDbAsync(async db =>
        {
            var deliveredOrder = await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == orderId);
            Assert.Equal("DELIVERED", deliveredOrder.Status);
            Assert.NotNull(deliveredOrder.ActualDeliveryTime);
            var dbDestSlot = await db.StationSlots.AsNoTracking().SingleAsync(s => s.Id == destSlot.Id);
            Assert.Equal("EMPTY", dbDestSlot.Status);
        });

        var dateStr = factory.Clock.Now.UtcDateTime.ToString("yyyy-MM-dd");
        using (var dashRes = await SendAsync(
                   HttpMethod.Get,
                   $"/api/analytics/dashboard?fromDate={dateStr}&toDate={dateStr}",
                   manager.Token))
        {
            Assert.Equal(HttpStatusCode.OK, dashRes.StatusCode);
            var dashJson = await dashRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(dashJson.GetProperty("successRate").GetProperty("deliveredCount").GetInt32() >= 1);
        }
    }

    [Fact]
    public async Task EndToEnd_failure_reporting_reschedule_recovery_and_external_v1_tracking_flow()
    {
        using var extFactory = new IntegrationApiFactory();
        using var adminClient = extFactory.CreateClient();
        var customerId = await IntegrationApiTests.ActorAsync(extFactory.Services, adminClient, "CUSTOMER");
        var orderBody = await IntegrationApiTests.ResourcesAsync(extFactory.Services, customerId);
        await IntegrationApiTests.ActorAsync(extFactory.Services, adminClient, "ADMIN");
        var apiKey = await IntegrationApiTests.ProvisionAsync(adminClient, customerId, extFactory.Clock);

        using var extClient = extFactory.CreateClient();
        extClient.DefaultRequestHeaders.Add("X-API-Key", apiKey.Secret);

        // 1. External Partner creates order via v1 API
        Guid orderId;
        using (var createRes = await extClient.PostAsJsonAsync(IntegrationApiTests.Orders, orderBody))
        {
            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
            var json = await createRes.Content.ReadFromJsonAsync<JsonElement>();
            orderId = json.GetProperty("id").GetGuid();
            Assert.Equal("PENDING", json.GetProperty("status").GetString());
        }

        // 2. Dispatcher approves order -> APPROVED
        var dispatcher = await ReviewData.UserAsync(extFactory.Services, "DISPATCHER");
        using (var approveRes = await SchedulingData.Send(adminClient, dispatcher.Token, $"/api/delivery-orders/{orderId}/approve", HttpMethod.Post))
        {
            Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);
        }

        // 3. Origin rejection triggers FAILED episode, then Dispatcher resolves with RESCHEDULE -> back to APPROVED
        Guid originStationId = Guid.Empty;
        string trackingNumber = "";
        await extFactory.WithDbAsync(async db =>
        {
            var o = await db.DeliveryOrders.AsNoTracking().SingleAsync(x => x.Id == orderId);
            originStationId = o.OriginStationId;
            trackingNumber = o.TrackingNumber;
        });

        var adminActor = await ReviewData.UserAsync(extFactory.Services, "ADMIN");
        using (var rejectRes = await SchedulingData.Send(
                   adminClient,
                   adminActor.Token,
                   $"/api/landing-stations/{originStationId}/operations/intake-reject",
                   HttpMethod.Post,
                   new { trackingNumber, reason = "Damaged outer tape" }))
        {
            Assert.Equal(HttpStatusCode.OK, rejectRes.StatusCode);
        }

        using (var resolveRes = await SchedulingData.Send(
                   adminClient,
                   dispatcher.Token,
                   $"/api/delivery-orders/{orderId}/resolve-failure",
                   HttpMethod.Post,
                   new { action = "RESCHEDULE", note = "Repackaged and verified", packageVerified = true }))
        {
            Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);
        }

        // 4. External Partner reads recovered order status & tracking via v1 API
        using (var extRead = await extClient.GetAsync($"{IntegrationApiTests.Orders}/{orderId}"))
        {
            Assert.Equal(HttpStatusCode.OK, extRead.StatusCode);
            var extJson = await extRead.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("APPROVED", extJson.GetProperty("status").GetString());
        }

        using (var extTracking = await extClient.GetAsync($"{IntegrationApiTests.Orders}/{orderId}/tracking"))
        {
            Assert.Equal(HttpStatusCode.OK, extTracking.StatusCode);
            var trkJson = await extTracking.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("APPROVED", trkJson.GetProperty("status").GetString());
        }
    }
}
