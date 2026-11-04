using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>LA-111: GET /api/delivery-orders and GET /api/delivery-orders/{id} (docs/api/customer-order-api.md sections 5.2 and 5.3).</summary>
public class DeliveryOrderQueryTests : IClassFixture<AuthApiFactory>, IDisposable
{
    private const string Password = "Passw0rd123";
    private static readonly string PasswordHash = new PasswordHasher().Hash(Password);
    private static int _counter;
    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public DeliveryOrderQueryTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private async Task<(Guid UserId, string Token)> SeedUserAsync(string role = AppRoles.Customer)
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"orderquery{n}@example.com";
        var userId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            var roleEntity = await db.Roles.SingleAsync(r => r.Code == role);
            db.Users.Add(new User
            {
                Id = userId,
                Email = email,
                FullName = $"Query User {n}",
                PhoneNumber = $"09{n:D8}",
                PasswordHash = PasswordHash,
                RoleId = roleEntity.Id,
                Status = "ACTIVE"
            });
            await db.SaveChangesAsync();
        });

        using var loginRes = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var doc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        return (userId, doc.GetProperty("accessToken").GetString()!);
    }

    private async Task<(Guid OriginId, Guid DestId)> SeedStationsAsync()
    {
        var originId = Guid.NewGuid();
        var destId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            db.LandingStations.AddRange(
                new LandingStation
                {
                    Id = originId,
                    StationCode = $"Q-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                    Name = "District 1 Hub",
                    Address = "1 Le Loi, District 1",
                    Latitude = 10.7769m,
                    Longitude = 106.7009m,
                    Status = "OPERATIONAL"
                },
                new LandingStation
                {
                    Id = destId,
                    StationCode = $"Q-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                    Name = "Thu Duc Hub",
                    Address = "2 Vo Van Ngan, Thu Duc",
                    Latitude = 10.8500m,
                    Longitude = 106.7700m,
                    Status = "OPERATIONAL"
                });
            await db.SaveChangesAsync();
        });

        return (originId, destId);
    }

    private async Task<(Guid OrderId, string TrackingNumber)> SeedOrderAsync(
        Guid customerId, Guid originId, Guid destId, string status = "PENDING")
    {
        var pkgId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var tracking = $"DD-202611-{Interlocked.Increment(ref _counter):D4}";
        var now = DateTime.UtcNow;

        await _factory.WithDbAsync(async db =>
        {
            db.Packages.Add(new Package
            {
                Id = pkgId,
                SenderId = customerId,
                PackageName = "Birthday gift",
                WeightGram = 850m,
                LengthCm = 25m,
                WidthCm = 18m,
                HeightCm = 10m,
                IsFragile = true,
                CreatedAt = now
            });
            db.DeliveryOrders.Add(new DeliveryOrder
            {
                Id = orderId,
                TrackingNumber = tracking,
                CustomerId = customerId,
                PackageId = pkgId,
                OriginStationId = originId,
                DestinationStationId = destId,
                RecipientName = "Tran Thi Binh",
                RecipientPhone = "0912345678",
                Status = status,
                PickupSecureCode = "483920",
                CreatedAt = now,
                UpdatedAt = now
            });
            db.DeliveryStatusLogs.Add(new DeliveryStatusLog
            {
                OrderId = orderId,
                ChangedBy = customerId,
                FromStatus = null,
                ToStatus = "PENDING",
                Remarks = "Order created",
                CreatedAt = now
            });
            await db.SaveChangesAsync();
        });

        return (orderId, tracking);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    [Fact]
    public async Task List_ReturnsContractPagedResultAndSummaryShape_AndFiltersByStatusAndKeyword()
    {
        var (userA, tokenA) = await SeedUserAsync();
        var (userB, _) = await SeedUserAsync();
        var (originId, destId) = await SeedStationsAsync();

        var (order1, tracking1) = await SeedOrderAsync(userA, originId, destId, "PENDING");
        await SeedOrderAsync(userA, originId, destId, "CANCELLED");
        await SeedOrderAsync(userB, originId, destId, "PENDING");

        using var req = Authorized(HttpMethod.Get, $"/api/delivery-orders?status=PENDING&searchKeyword={tracking1}", tokenA);
        using var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();

        // PagedResult contract shape: items, page, pageSize, totalCount, totalPages (no hasNext/hasPrevious)
        Assert.True(json.TryGetProperty("items", out var items));
        Assert.Equal(1, json.GetProperty("page").GetInt32());
        Assert.Equal(20, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, json.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, json.GetProperty("totalPages").GetInt32());
        Assert.False(json.TryGetProperty("hasNext", out _));
        Assert.False(json.TryGetProperty("hasPrevious", out _));

        // DeliveryOrderSummary contract shape: no customerId, customerName, weightGram or pickupSecureCode
        var item = Assert.Single(items.EnumerateArray());
        Assert.Equal(order1.ToString(), item.GetProperty("id").GetString());
        Assert.Equal(tracking1, item.GetProperty("trackingNumber").GetString());
        Assert.False(item.TryGetProperty("customerId", out _));
        Assert.False(item.TryGetProperty("customerName", out _));
        Assert.False(item.TryGetProperty("weightGram", out _));
        Assert.False(item.TryGetProperty("pickupSecureCode", out _));
    }

    [Fact]
    public async Task GetById_ReturnsContractDetailShape_NeverExposesPickupSecureCode_AndHidesOtherCustomersOrders()
    {
        var (userA, tokenA) = await SeedUserAsync();
        var (_, tokenB) = await SeedUserAsync();
        var (originId, destId) = await SeedStationsAsync();
        var (orderId, _) = await SeedOrderAsync(userA, originId, destId, "PENDING");

        using var req = Authorized(HttpMethod.Get, $"/api/delivery-orders/{orderId}", tokenA);
        using var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();

        // Security rule BR-LS-22: pickupSecureCode is NEVER returned in order detail
        Assert.False(json.TryGetProperty("pickupSecureCode", out _));
        Assert.True(json.TryGetProperty("package", out _));
        Assert.True(json.TryGetProperty("originStation", out var originStation));
        Assert.Equal("OPERATIONAL", originStation.GetProperty("status").GetString());
        Assert.True(json.TryGetProperty("destinationStation", out _));

        // StatusLog contract shape: fromStatus, toStatus, remarks, createdAt (no id, orderId, changedBy)
        var log = Assert.Single(json.GetProperty("statusLogs").EnumerateArray());
        Assert.Equal("PENDING", log.GetProperty("toStatus").GetString());
        Assert.False(log.TryGetProperty("id", out _));
        Assert.False(log.TryGetProperty("orderId", out _));
        Assert.False(log.TryGetProperty("changedBy", out _));

        // Other customer gets 404 Not Found (BR-CO-36)
        using var otherReq = Authorized(HttpMethod.Get, $"/api/delivery-orders/{orderId}", tokenB);
        using var otherRes = await _client.SendAsync(otherReq);
        Assert.Equal(HttpStatusCode.NotFound, otherRes.StatusCode);
    }

    [Fact]
    public async Task List_WithInvalidFilters_Returns400BadRequest()
    {
        var (_, token) = await SeedUserAsync();

        using var badStatus = Authorized(HttpMethod.Get, "/api/delivery-orders?status=UNKNOWN", token);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(badStatus)).StatusCode);

        var from = DateTime.UtcNow.AddDays(1).ToString("O");
        var to = DateTime.UtcNow.AddDays(-1).ToString("O");
        using var badDate = Authorized(HttpMethod.Get, $"/api/delivery-orders?fromDate={from}&toDate={to}", token);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(badDate)).StatusCode);
    }
}
