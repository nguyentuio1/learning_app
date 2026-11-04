using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>LA-107: PUT /api/delivery-orders/{id} and POST /api/delivery-orders/{id}/cancel (docs/api/customer-order-api.md sections 5.4 and 5.5).</summary>
public class DeliveryOrderMutationTests : IClassFixture<AuthApiFactory>, IDisposable
{
    private const string Password = "Passw0rd123";
    private static readonly string PasswordHash = new PasswordHasher().Hash(Password);
    private static int _counter;
    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public DeliveryOrderMutationTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private async Task<(Guid UserId, string Token)> SeedUserAsync(string role = AppRoles.Customer)
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"ordermutate{n}@example.com";
        var userId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            var roleEntity = await db.Roles.SingleAsync(r => r.Code == role);
            db.Users.Add(new User
            {
                Id = userId,
                Email = email,
                FullName = $"Mutation User {n}",
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

    private async Task<(Guid OriginId, Guid DestId, Guid AltDestId, Guid MaintId)> SeedStationsAsync()
    {
        var originId = Guid.NewGuid();
        var destId = Guid.NewGuid();
        var altDestId = Guid.NewGuid();
        var maintId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            db.LandingStations.AddRange(
                new LandingStation
                {
                    Id = originId,
                    StationCode = $"M-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                    Name = "Origin Hub",
                    Address = "1 Origin St",
                    Latitude = 10.77m,
                    Longitude = 106.70m,
                    Status = "OPERATIONAL"
                },
                new LandingStation
                {
                    Id = destId,
                    StationCode = $"M-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                    Name = "Dest Hub 1",
                    Address = "2 Dest St",
                    Latitude = 10.78m,
                    Longitude = 106.71m,
                    Status = "OPERATIONAL"
                },
                new LandingStation
                {
                    Id = altDestId,
                    StationCode = $"M-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                    Name = "Dest Hub 2",
                    Address = "3 Alt Dest St",
                    Latitude = 10.79m,
                    Longitude = 106.72m,
                    Status = "OPERATIONAL"
                },
                new LandingStation
                {
                    Id = maintId,
                    StationCode = $"M-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                    Name = "Maint Hub",
                    Address = "4 Maint St",
                    Latitude = 10.80m,
                    Longitude = 106.73m,
                    Status = "MAINTENANCE"
                });
            await db.SaveChangesAsync();
        });

        return (originId, destId, altDestId, maintId);
    }

    private async Task<Guid> SeedAddressAsync(Guid userId)
    {
        var addrId = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            db.CustomerAddresses.Add(new CustomerAddress
            {
                Id = addrId,
                UserId = userId,
                Label = "Home",
                ContactName = "Nguyen Van An",
                ContactPhone = "0901234567",
                AddressLine = "12 Nguyen Hue, District 1",
                IsDefault = true
            });
            await db.SaveChangesAsync();
        });
        return addrId;
    }

    private async Task<Guid> SeedOrderAsync(Guid customerId, Guid originId, Guid destId, Guid? addressId = null, string status = "PENDING")
    {
        var pkgId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await _factory.WithDbAsync(async db =>
        {
            db.Packages.Add(new Package
            {
                Id = pkgId,
                SenderId = customerId,
                PackageName = "Gift Box",
                WeightGram = 800m,
                LengthCm = 20m,
                WidthCm = 15m,
                HeightCm = 10m,
                IsFragile = true,
                CreatedAt = now
            });
            db.DeliveryOrders.Add(new DeliveryOrder
            {
                Id = orderId,
                TrackingNumber = $"DD-202611-{Interlocked.Increment(ref _counter):D4}",
                CustomerId = customerId,
                PackageId = pkgId,
                OriginStationId = originId,
                DestinationStationId = destId,
                DeliveryAddressId = addressId,
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

        return orderId;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    [Fact]
    public async Task Update_PendingOrder_ReturnsFullDetailWithoutPickupCode_AndCanClearAddress()
    {
        var (userId, token) = await SeedUserAsync();
        var (originId, destId, altDestId, _) = await SeedStationsAsync();
        var addrId = await SeedAddressAsync(userId);
        var orderId = await SeedOrderAsync(userId, originId, destId, addrId);

        using var putReq = Authorized(HttpMethod.Put, $"/api/delivery-orders/{orderId}", token);
        putReq.Content = JsonContent.Create(new UpdateDeliveryOrderRequest("Le Van C", "0987654321", null, altDestId));
        using var res = await _client.SendAsync(putReq);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.TryGetProperty("pickupSecureCode", out _));
        Assert.True(json.TryGetProperty("package", out _));
        Assert.True(json.TryGetProperty("originStation", out _));
        Assert.True(json.TryGetProperty("destinationStation", out _));
        Assert.True(json.TryGetProperty("statusLogs", out _));

        var detail = json.Deserialize<DeliveryOrderDetailDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(detail);
        Assert.Equal("Le Van C", detail.RecipientName);
        Assert.Equal("0987654321", detail.RecipientPhone);
        Assert.Equal(altDestId, detail.DestinationStationId);
        Assert.Equal("Dest Hub 2", detail.DestinationStationName);
        Assert.Null(detail.DeliveryAddressId);
        Assert.Null(detail.DeliveryAddress);
    }

    [Fact]
    public async Task Update_RejectsUnknownFields_AndValidatesStationsAndOwnership()
    {
        var (userA, tokenA) = await SeedUserAsync();
        var (userB, tokenB) = await SeedUserAsync();
        var (originId, destId, altDestId, maintId) = await SeedStationsAsync();
        var orderId = await SeedOrderAsync(userA, originId, destId);

        // 1. Unknown field rejected with 400 (BR-CO-19)
        using var badFieldReq = Authorized(HttpMethod.Put, $"/api/delivery-orders/{orderId}", tokenA);
        badFieldReq.Content = new StringContent(
            $"{{\"recipientName\":\"A\",\"recipientPhone\":\"0912345678\",\"destinationStationId\":\"{altDestId}\",\"status\":\"DELIVERED\"}}",
            Encoding.UTF8, "application/json");
        using var badFieldRes = await _client.SendAsync(badFieldReq);
        Assert.Equal(HttpStatusCode.BadRequest, badFieldRes.StatusCode);

        // 2. Destination equals origin -> 400
        using var sameStationReq = Authorized(HttpMethod.Put, $"/api/delivery-orders/{orderId}", tokenA);
        sameStationReq.Content = JsonContent.Create(new UpdateDeliveryOrderRequest("A", "0912345678", null, originId));
        using var sameStationRes = await _client.SendAsync(sameStationReq);
        Assert.Equal(HttpStatusCode.BadRequest, sameStationRes.StatusCode);

        // 3. Maintenance destination station -> 400
        using var maintReq = Authorized(HttpMethod.Put, $"/api/delivery-orders/{orderId}", tokenA);
        maintReq.Content = JsonContent.Create(new UpdateDeliveryOrderRequest("A", "0912345678", null, maintId));
        using var maintRes = await _client.SendAsync(maintReq);
        Assert.Equal(HttpStatusCode.BadRequest, maintRes.StatusCode);

        // 4. Other customer -> 404
        using var otherReq = Authorized(HttpMethod.Put, $"/api/delivery-orders/{orderId}", tokenB);
        otherReq.Content = JsonContent.Create(new UpdateDeliveryOrderRequest("A", "0912345678", null, altDestId));
        using var otherRes = await _client.SendAsync(otherReq);
        Assert.Equal(HttpStatusCode.NotFound, otherRes.StatusCode);
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("APPROVED")]
    public async Task Cancel_PendingOrApprovedOrder_ReturnsFullDetailAndWritesStatusLogAndNotification(string initialStatus)
    {
        var (userId, token) = await SeedUserAsync();
        var (originId, destId, _, _) = await SeedStationsAsync();
        var orderId = await SeedOrderAsync(userId, originId, destId, status: initialStatus);

        // Legacy "reason" field is not accepted; only "cancellationReason" (contract 5.5)
        using var legacyReasonReq = Authorized(HttpMethod.Post, $"/api/delivery-orders/{orderId}/cancel", token);
        legacyReasonReq.Content = JsonContent.Create(new { reason = "Legacy field" });
        using var legacyReasonRes = await _client.SendAsync(legacyReasonReq);
        Assert.Equal(HttpStatusCode.BadRequest, legacyReasonRes.StatusCode);

        using var cancelReq = Authorized(HttpMethod.Post, $"/api/delivery-orders/{orderId}/cancel", token);
        cancelReq.Content = JsonContent.Create(new { cancellationReason = "Recipient is not available." });
        using var res = await _client.SendAsync(cancelReq);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.TryGetProperty("pickupSecureCode", out _));

        var detail = json.Deserialize<DeliveryOrderDetailDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(detail);
        Assert.Equal("CANCELLED", detail.Status);
        Assert.Equal("Recipient is not available.", detail.CancelReason);
        Assert.Equal(2, detail.StatusLogs.Count);
        Assert.Equal(initialStatus, detail.StatusLogs[1].FromStatus);
        Assert.Equal("CANCELLED", detail.StatusLogs[1].ToStatus);
        Assert.Equal("Recipient is not available.", detail.StatusLogs[1].Remarks);

        // Repeated cancel returns 409 Conflict and creates no extra log
        using var repeatReq = Authorized(HttpMethod.Post, $"/api/delivery-orders/{orderId}/cancel", token);
        repeatReq.Content = JsonContent.Create(new { cancellationReason = "Again" });
        using var repeatRes = await _client.SendAsync(repeatReq);
        Assert.Equal(HttpStatusCode.Conflict, repeatRes.StatusCode);
    }

    [Fact]
    public async Task Cancel_ConcurrentRequestsOnSameOrder_OnlyOneSucceedsAndOthersReturn409()
    {
        var (userId, token) = await SeedUserAsync();
        var (originId, destId, _, _) = await SeedStationsAsync();
        var orderId = await SeedOrderAsync(userId, originId, destId, status: "PENDING");

        var tasks = Enumerable.Range(1, 5).Select(async _ =>
        {
            using var req = Authorized(HttpMethod.Post, $"/api/delivery-orders/{orderId}/cancel", token);
            req.Content = JsonContent.Create(new CancelDeliveryOrderRequest("Concurrent cancel"));
            using var res = await _client.SendAsync(req);
            return res.StatusCode;
        });

        var results = await Task.WhenAll(tasks);
        Assert.Single(results, s => s == HttpStatusCode.OK);
        Assert.Equal(4, results.Count(s => s == HttpStatusCode.Conflict));

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == orderId && l.ToStatus == "CANCELLED"));
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.OrderId == orderId));
        });
    }
}
