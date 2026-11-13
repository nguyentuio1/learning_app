using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>
/// LA-125: the station operations lock the order row (SELECT ... FOR UPDATE) inside a transaction, which only runs
/// on a relational database. These tests run on PostgreSQL and are skipped when TEST_POSTGRES_CONNECTION is not set.
/// Requests are released at the same moment; the assertions hold for every possible order of events.
/// </summary>
public class StationOperationsConcurrencyTests : IClassFixture<PostgresApiFactory>, IDisposable
{
    private const string Password = "Passw0rd123";
    private const int Rounds = 10;
    private static readonly string PasswordHash = new PasswordHasher().Hash(Password);
    private static int _counter;

    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _client;

    public StationOperationsConcurrencyTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _client = PostgresApiFactory.ServerConnectionString is null ? new HttpClient() : factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    // ---------- scenarios ----------

    [PostgresFact]
    public async Task Intake_and_reject_of_the_same_order_at_the_same_time_leave_order_and_slot_consistent()
    {
        for (var round = 1; round <= Rounds; round++)
        {
            var s = await SeedOriginScenarioAsync();

            var responses = await FireTogetherAsync(
                () => PostAsync($"/api/landing-stations/{s.OriginId}/operations/intake", s.Token, new
                {
                    trackingNumber = s.Tracking, slotId = s.SlotId,
                    measuredWeightGram = 500, measuredLengthCm = 20, measuredWidthCm = 10, measuredHeightCm = 5,
                    visualInspectionPassed = true
                }),
                () => PostAsync($"/api/landing-stations/{s.OriginId}/operations/intake-reject", s.Token,
                    new { trackingNumber = s.Tracking, reason = "Seal is damaged." }));

            var codes = responses.Select(r => r.StatusCode).OrderBy(c => c).ToList();
            Assert.True(codes.SequenceEqual(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }),
                $"round {round}: expected one 200 and one 409 but got {string.Join(", ", codes)}");

            await _factory.WithDbAsync(async db =>
            {
                var order = await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == s.OrderId);
                var slot = await db.StationSlots.AsNoTracking().SingleAsync(x => x.Id == s.SlotId);
                var logs = await db.DeliveryStatusLogs.Where(l => l.OrderId == s.OrderId).ToListAsync();

                Assert.Single(logs);
                if (order.Status == "READY_FOR_TAKEOFF")
                    Assert.Equal("OCCUPIED", slot.Status);
                else
                {
                    Assert.Equal("FAILED", order.Status);
                    Assert.Equal("EMPTY", slot.Status);
                }
                Assert.Equal(order.Status, logs[0].ToStatus);
            });
        }
    }

    [PostgresFact]
    public async Task Several_rejects_of_the_same_order_at_the_same_time_fail_the_order_once()
    {
        const int simultaneous = 6;
        for (var round = 1; round <= Rounds; round++)
        {
            var s = await SeedOriginScenarioAsync();

            var responses = await FireTogetherAsync(Enumerable.Range(0, simultaneous)
                .Select(i => (Func<Task<HttpResponseMessage>>)(() => PostAsync(
                    $"/api/landing-stations/{s.OriginId}/operations/intake-reject", s.Token,
                    new { trackingNumber = s.Tracking, reason = $"Too heavy {round}-{i}" }))).ToArray());
            var codes = responses.Select(r => r.StatusCode).ToList();

            Assert.True(codes.Count(c => c == HttpStatusCode.OK) == 1,
                $"round {round}: expected exactly one 200 but got {string.Join(", ", codes)}");
            Assert.True(codes.Count(c => c == HttpStatusCode.Conflict) == simultaneous - 1,
                $"round {round}: the other requests must be 409 but got {string.Join(", ", codes)}");

            await _factory.WithDbAsync(async db =>
            {
                Assert.Equal("FAILED", (await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == s.OrderId)).Status);
                Assert.Equal(1, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == s.OrderId));
                Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "PACKAGE_INTAKE_REJECT" && a.EntityId == s.OrderId.ToString()));
                Assert.Equal(1, await db.Notifications.CountAsync(n => n.OrderId == s.OrderId));
            });
        }
    }

    [PostgresFact]
    public async Task Pickup_and_the_timeout_job_at_the_same_time_never_both_win()
    {
        for (var round = 1; round <= Rounds; round++)
        {
            var s = await SeedDestinationScenarioAsync(arrivedHoursAgo: 30);

            var responses = await FireTogetherAsync(
                () => PostAsync($"/api/landing-stations/{s.DestinationId}/operations/pickup", s.Token,
                    new { trackingNumber = s.Tracking, pickupSecureCode = "123456" }),
                async () =>
                {
                    using var scope = _factory.Services.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IPickupTimeoutService>();
                    await service.ExpireOverdueAsync(DateTime.UtcNow);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });
            var pickupStatus = responses[0].StatusCode;

            await _factory.WithDbAsync(async db =>
            {
                var order = await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == s.OrderId);
                var slot = await db.StationSlots.AsNoTracking().SingleAsync(x => x.Id == s.SlotId);
                var timeouts = await db.AuditLogs.CountAsync(a => a.Action == "ORDER_PICKUP_TIMEOUT" && a.EntityId == s.OrderId.ToString());
                var deliveries = await db.AuditLogs.CountAsync(a => a.Action == "PACKAGE_DELIVERY_CONFIRMATION" && a.EntityId == s.OrderId.ToString());

                if (order.Status == "DELIVERED")
                {
                    Assert.Equal(HttpStatusCode.OK, pickupStatus);
                    Assert.Equal("EMPTY", slot.Status);
                    Assert.Equal(0, timeouts);
                    Assert.Equal(1, deliveries);
                }
                else
                {
                    Assert.Equal("FAILED", order.Status);
                    Assert.Equal(HttpStatusCode.Conflict, pickupStatus);
                    Assert.Equal("OCCUPIED", slot.Status);
                    Assert.Equal(1, timeouts);
                    Assert.Equal(0, deliveries);
                }
            });
        }
    }

    // ---------- helpers ----------

    private record Scenario(Guid OrderId, string Tracking, Guid OriginId, Guid DestinationId, Guid SlotId, string Token);

    private async Task<string> SeedOperatorAsync(Guid userId)
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"opsconc{n}@example.com";
        await _factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.SingleAsync(r => r.Code == AppRoles.StationOperator);
            db.Users.Add(new User
            {
                Id = userId, Email = email, FullName = $"Ops {n}", PhoneNumber = $"07{n:D8}",
                PasswordHash = PasswordHash, RoleId = role.Id, Status = "ACTIVE"
            });
            await db.SaveChangesAsync();
        });
        using var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private async Task<Guid> SeedCustomerAsync()
    {
        var n = Interlocked.Increment(ref _counter);
        var id = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.SingleAsync(r => r.Code == AppRoles.Customer);
            db.Users.Add(new User
            {
                Id = id, Email = $"custconc{n}@example.com", FullName = $"Cust {n}", PhoneNumber = $"06{n:D8}",
                PasswordHash = PasswordHash, RoleId = role.Id, Status = "ACTIVE"
            });
            await db.SaveChangesAsync();
        });
        return id;
    }

    private static LandingStation NewStation(string prefix, int n, Guid? operatorId) => new()
    {
        Id = Guid.NewGuid(), StationCode = $"{prefix}{n:D5}", Name = $"{prefix} {n}", Address = $"{n} St",
        Latitude = 10.7m, Longitude = 106.7m, TotalSlots = 2, Status = "OPERATIONAL", OperatorId = operatorId
    };

    /// <summary>An APPROVED order at an origin station with one EMPTY slot, and the operator of that station.</summary>
    private async Task<Scenario> SeedOriginScenarioAsync()
    {
        var n = Interlocked.Increment(ref _counter);
        var operatorId = Guid.NewGuid();
        var token = await SeedOperatorAsync(operatorId);
        var customerId = await SeedCustomerAsync();
        var origin = NewStation("OR", n, operatorId);
        var dest = NewStation("DE", n, null);
        var slot = new StationSlot { Id = Guid.NewGuid(), StationId = origin.Id, SlotNumber = 1, Status = "EMPTY" };
        var package = new Package { SenderId = customerId, PackageName = "Box", WeightGram = 400, LengthCm = 15, WidthCm = 8, HeightCm = 4 };
        var order = new DeliveryOrder
        {
            TrackingNumber = $"DD-OC-{n:D5}", CustomerId = customerId, Package = package,
            OriginStationId = origin.Id, DestinationStationId = dest.Id, RecipientName = "R",
            RecipientPhone = "0909000000", PickupSecureCode = "123456", Status = "APPROVED",
            ScheduledDepartureTime = DateTime.UtcNow.AddHours(2), EstimatedDeliveryTime = DateTime.UtcNow.AddHours(2).AddMinutes(10)
        };
        var destinationSlot = new StationSlot { StationId = dest.Id, SlotNumber = 1, Status = "RESERVED" };
        var drone = new Drone { SerialNumber = $"ORIGIN-DRN-{n}", ModelName = "Carrier", MaxPayloadGram = 3000, MaxFlightRangeKm = 30, Status = "ASSIGNED", CurrentStationId = origin.Id };
        var mission = new FlightMission { Order = order, Drone = drone, DestinationSlot = destinationSlot };
        await _factory.WithDbAsync(async db =>
        {
            db.LandingStations.AddRange(origin, dest);
            db.StationSlots.AddRange(slot, destinationSlot);
            db.FlightMissions.Add(mission);
            db.DeliveryOrders.Add(order);
            await db.SaveChangesAsync();
        });
        return new Scenario(order.Id, order.TrackingNumber, origin.Id, dest.Id, slot.Id, token);
    }

    /// <summary>An ARRIVED order at a destination station, its OCCUPIED slot linked by a mission, and the operator of that station.</summary>
    private async Task<Scenario> SeedDestinationScenarioAsync(double arrivedHoursAgo)
    {
        var n = Interlocked.Increment(ref _counter);
        var operatorId = Guid.NewGuid();
        var token = await SeedOperatorAsync(operatorId);
        var customerId = await SeedCustomerAsync();
        var origin = NewStation("OR", n, null);
        var dest = NewStation("DE", n, operatorId);
        var slot = new StationSlot { Id = Guid.NewGuid(), StationId = dest.Id, SlotNumber = 1, Status = "OCCUPIED" };
        var package = new Package { SenderId = customerId, PackageName = "Box", WeightGram = 400, LengthCm = 15, WidthCm = 8, HeightCm = 4 };
        var order = new DeliveryOrder
        {
            TrackingNumber = $"DD-OC-{n:D5}", CustomerId = customerId, Package = package,
            OriginStationId = origin.Id, DestinationStationId = dest.Id, RecipientName = "R",
            RecipientPhone = "0909000000", PickupSecureCode = "123456", Status = "ARRIVED"
        };
        var drone = new Drone { SerialNumber = $"DRN-OC-{n}", ModelName = "X", MaxPayloadGram = 2500, MaxFlightRangeKm = 30, BatteryCapacityMah = 18000 };
        var mission = new FlightMission { Order = order, Drone = drone, DestinationSlot = slot, FlightStatus = "COMPLETED" };
        await _factory.WithDbAsync(async db =>
        {
            db.LandingStations.AddRange(origin, dest);
            db.StationSlots.Add(slot);
            db.Drones.Add(drone);
            db.DeliveryOrders.Add(order);
            db.FlightMissions.Add(mission);
            db.DeliveryStatusLogs.Add(new DeliveryStatusLog
            {
                OrderId = order.Id, FromStatus = "IN_TRANSIT", ToStatus = "ARRIVED",
                CreatedAt = DateTime.UtcNow.AddHours(-arrivedHoursAgo)
            });
            await db.SaveChangesAsync();
        });
        return new Scenario(order.Id, order.TrackingNumber, origin.Id, dest.Id, slot.Id, token);
    }

    private Task<HttpResponseMessage> PostAsync(string url, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage[]> FireTogetherAsync(params Func<Task<HttpResponseMessage>>[] calls)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = calls.Select(call => Task.Run(async () =>
        {
            await gate.Task;
            return await call();
        })).ToArray();
        await Task.Delay(100); // let every task reach the gate
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }
}
