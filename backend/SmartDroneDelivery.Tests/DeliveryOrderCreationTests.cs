using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class DeliveryOrderCreationTests : IClassFixture<AuthApiFactory>, IDisposable
{
    private const string Password = "Passw0rd123";
    private static readonly string PasswordHash = new PasswordHasher().Hash(Password);
    private static int _counter;
    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public DeliveryOrderCreationTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private async Task<(Guid UserId, string Token)> SeedUserAsync(string role = AppRoles.Customer)
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"ordercreate{n}@example.com";
        var userId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            var roleEntity = await db.Roles.SingleAsync(r => r.Code == role);
            db.Users.Add(new User
            {
                Id = userId,
                Email = email,
                FullName = $"Test User {n}",
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
        var token = doc.GetProperty("accessToken").GetString()!;
        return (userId, token);
    }

    private async Task<(Guid OriginId, Guid DestId, Guid MaintenanceId)> SeedStationsAsync()
    {
        var originId = Guid.NewGuid();
        var destId = Guid.NewGuid();
        var maintenanceId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            db.LandingStations.AddRange(
                new LandingStation
                {
                    Id = originId,
                    StationCode = $"ST-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                    Name = "Origin Station Alpha",
                    Address = "123 Alpha Road",
                    Latitude = 10.7769m,
                    Longitude = 106.7009m,
                    Status = "OPERATIONAL"
                },
                new LandingStation
                {
                    Id = destId,
                    StationCode = $"ST-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                    Name = "Destination Station Beta",
                    Address = "456 Beta Boulevard",
                    Latitude = 10.7800m,
                    Longitude = 106.6900m,
                    Status = "OPERATIONAL"
                },
                new LandingStation
                {
                    Id = maintenanceId,
                    StationCode = $"ST-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                    Name = "Maintenance Station Gamma",
                    Address = "789 Gamma Street",
                    Latitude = 10.7900m,
                    Longitude = 106.6800m,
                    Status = "MAINTENANCE"
                });

            await db.SaveChangesAsync();
        });

        return (originId, destId, maintenanceId);
    }

    private async Task<Guid> SeedPackageDirectAsync(Guid senderId, decimal weightGram = 500m, decimal lengthCm = 15m, decimal widthCm = 10m, decimal heightCm = 8m)
    {
        var pkgId = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            db.Packages.Add(new Package
            {
                Id = pkgId,
                SenderId = senderId,
                PackageName = "Seeded Package",
                WeightGram = weightGram,
                LengthCm = lengthCm,
                WidthCm = widthCm,
                HeightCm = heightCm,
                IsFragile = false,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });
        return pkgId;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task CreateOrder_WithoutToken_Returns401Unauthorized()
    {
        var req = new CreateDeliveryOrderRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Recipient", "0912345678", null);
        using var res = await _client.PostAsJsonAsync("/api/delivery-orders", req);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData(AppRoles.Dispatcher)]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.StationOperator)]
    [InlineData(AppRoles.Manager)]
    public async Task CreateOrder_WithNonCustomerRole_Returns403Forbidden(string role)
    {
        var (_, token) = await SeedUserAsync(role);
        var (originId, destId, _) = await SeedStationsAsync();

        using var req = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        req.Content = JsonContent.Create(new CreateDeliveryOrderRequest(Guid.NewGuid(), originId, destId, "Recipient", "0912345678", null));
        using var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithInactiveCustomerAccount_Returns403Forbidden()
    {
        var (userId, token) = await SeedUserAsync(AppRoles.Customer);
        var (originId, destId, _) = await SeedStationsAsync();
        var pkgId = await SeedPackageDirectAsync(userId);

        await _factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.Status = "INACTIVE";
            await db.SaveChangesAsync();
        });

        using var req = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        req.Content = JsonContent.Create(new CreateDeliveryOrderRequest(pkgId, originId, destId, "Recipient", "0912345678", null));
        using var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [InlineData(3001, 10, 10, 10, "weightGram")]
    [InlineData(500, 31, 10, 10, "lengthCm")]
    [InlineData(500, 10, 21, 10, "widthCm")]
    [InlineData(500, 10, 10, 16, "heightCm")]
    public async Task CreateOrder_WithOverweightOrOversizedPackage_Returns400BadRequestWithFieldError(
        decimal weight, decimal length, decimal width, decimal height, string expectedErrorField)
    {
        var (userId, token) = await SeedUserAsync();
        var (originId, destId, _) = await SeedStationsAsync();
        var pkgId = await SeedPackageDirectAsync(userId, weight, length, width, height);

        using var req = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        req.Content = JsonContent.Create(new CreateDeliveryOrderRequest(pkgId, originId, destId, "Recipient", "0912345678", null));
        using var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty(expectedErrorField, out _));
    }

    [Fact]
    public async Task CreateOrder_WhenPackageAlreadyLinkedOrCancelled_Returns409Conflict()
    {
        var (userId, token) = await SeedUserAsync();
        var (originId, destId, _) = await SeedStationsAsync();
        var pkgId = await SeedPackageDirectAsync(userId);

        var createReq = new CreateDeliveryOrderRequest(pkgId, originId, destId, "Recipient", "0912345678", null);

        // First order succeeds
        using var req1 = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        req1.Content = JsonContent.Create(createReq);
        using var res1 = await _client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);
        var order = await res1.Content.ReadFromJsonAsync<CreatedDeliveryOrderDto>();

        // Reusing while PENDING -> 409 Conflict
        using var req2 = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        req2.Content = JsonContent.Create(createReq);
        using var res2 = await _client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);

        // Cancel the order, then try reusing the same package -> still 409 Conflict (BR-CO-08)
        await _factory.WithDbAsync(async db =>
        {
            var dbOrder = await db.DeliveryOrders.SingleAsync(o => o.Id == order!.Id);
            dbOrder.Status = "CANCELLED";
            dbOrder.CancelReason = "Customer cancelled";
            await db.SaveChangesAsync();
        });

        using var req3 = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        req3.Content = JsonContent.Create(createReq);
        using var res3 = await _client.SendAsync(req3);
        Assert.Equal(HttpStatusCode.Conflict, res3.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithMissingOrOtherCustomersPackageOrAddress_Returns404NotFound()
    {
        var (userA, tokenA) = await SeedUserAsync();
        var (userB, _) = await SeedUserAsync();
        var (originId, destId, _) = await SeedStationsAsync();
        var pkgA = await SeedPackageDirectAsync(userA);
        var pkgB = await SeedPackageDirectAsync(userB);

        Guid addrB = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            db.CustomerAddresses.Add(new CustomerAddress
            {
                Id = addrB,
                UserId = userB,
                Label = "User B Home",
                ContactName = "Bob",
                ContactPhone = "0912345678",
                AddressLine = "99 Bob St",
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        // 1. Other customer's package -> 404
        using var reqStealPkg = Authorized(HttpMethod.Post, "/api/delivery-orders", tokenA);
        reqStealPkg.Content = JsonContent.Create(new CreateDeliveryOrderRequest(pkgB, originId, destId, "Recipient", "0912345678", null));
        using var resStealPkg = await _client.SendAsync(reqStealPkg);
        Assert.Equal(HttpStatusCode.NotFound, resStealPkg.StatusCode);

        // 2. Non-existent deliveryAddressId -> 404
        using var reqMissingAddr = Authorized(HttpMethod.Post, "/api/delivery-orders", tokenA);
        reqMissingAddr.Content = JsonContent.Create(new CreateDeliveryOrderRequest(pkgA, originId, destId, "Recipient", "0912345678", Guid.NewGuid()));
        using var resMissingAddr = await _client.SendAsync(reqMissingAddr);
        Assert.Equal(HttpStatusCode.NotFound, resMissingAddr.StatusCode);

        // 3. Other customer's deliveryAddressId -> 404
        using var reqOtherAddr = Authorized(HttpMethod.Post, "/api/delivery-orders", tokenA);
        reqOtherAddr.Content = JsonContent.Create(new CreateDeliveryOrderRequest(pkgA, originId, destId, "Recipient", "0912345678", addrB));
        using var resOtherAddr = await _client.SendAsync(reqOtherAddr);
        Assert.Equal(HttpStatusCode.NotFound, resOtherAddr.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_StationAndRecipientValidation_ReturnsExpectedStatusCodes()
    {
        var (userId, token) = await SeedUserAsync();
        var (originId, destId, maintenanceId) = await SeedStationsAsync();
        var pkgId = await SeedPackageDirectAsync(userId);

        // 1. Missing origin station -> 404
        using var reqMissingOrigin = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        reqMissingOrigin.Content = JsonContent.Create(new CreateDeliveryOrderRequest(pkgId, Guid.NewGuid(), destId, "Recipient", "0912345678", null));
        using var resMissingOrigin = await _client.SendAsync(reqMissingOrigin);
        Assert.Equal(HttpStatusCode.NotFound, resMissingOrigin.StatusCode);

        // 2. Missing destination station -> 404
        using var reqMissingDest = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        reqMissingDest.Content = JsonContent.Create(new CreateDeliveryOrderRequest(pkgId, originId, Guid.NewGuid(), "Recipient", "0912345678", null));
        using var resMissingDest = await _client.SendAsync(reqMissingDest);
        Assert.Equal(HttpStatusCode.NotFound, resMissingDest.StatusCode);

        // 3. Non-operational station -> 400 with field error
        using var reqMaint = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        reqMaint.Content = JsonContent.Create(new CreateDeliveryOrderRequest(pkgId, maintenanceId, destId, "Recipient", "0912345678", null));
        using var resMaint = await _client.SendAsync(reqMaint);
        Assert.Equal(HttpStatusCode.BadRequest, resMaint.StatusCode);

        // 4. Same origin and destination -> 400
        using var reqSame = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        reqSame.Content = JsonContent.Create(new CreateDeliveryOrderRequest(pkgId, originId, originId, "Recipient", "0912345678", null));
        using var resSame = await _client.SendAsync(reqSame);
        Assert.Equal(HttpStatusCode.BadRequest, resSame.StatusCode);

        // 5. Invalid recipientPhone ("1") -> 400
        using var reqBadPhone = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        reqBadPhone.Content = JsonContent.Create(new CreateDeliveryOrderRequest(pkgId, originId, destId, "Recipient", "1", null));
        using var resBadPhone = await _client.SendAsync(reqBadPhone);
        Assert.Equal(HttpStatusCode.BadRequest, resBadPhone.StatusCode);

        // 6. Blank recipientName -> 400
        using var reqBlankName = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        reqBlankName.Content = JsonContent.Create(new CreateDeliveryOrderRequest(pkgId, originId, destId, "   ", "0912345678", null));
        using var resBlankName = await _client.SendAsync(reqBlankName);
        Assert.Equal(HttpStatusCode.BadRequest, resBlankName.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_HappyPath_GeneratesTracking_PickupCode_StatusLog_And_Notification()
    {
        var (userId, token) = await SeedUserAsync();
        var (originId, destId, _) = await SeedStationsAsync();
        var pkgId = await SeedPackageDirectAsync(userId);

        using var req = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        req.Content = JsonContent.Create(new CreateDeliveryOrderRequest(pkgId, originId, destId, "Alice Receiver", "0912345678", null));
        using var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var order = await res.Content.ReadFromJsonAsync<CreatedDeliveryOrderDto>();
        Assert.NotNull(order);
        Assert.Equal("PENDING", order.Status);
        Assert.Matches(new Regex(@"^DD-\d{6}-\d{4,5}$"), order.TrackingNumber);
        Assert.NotNull(order.PickupSecureCode);
        Assert.Matches(new Regex(@"^\d{6}$"), order.PickupSecureCode!);
        Assert.Single(order.StatusLogs);
        Assert.Null(order.StatusLogs[0].FromStatus);
        Assert.Equal("PENDING", order.StatusLogs[0].ToStatus);

        await _factory.WithDbAsync(async db =>
        {
            var notif = await db.Notifications.FirstOrDefaultAsync(n => n.OrderId == order.Id && n.UserId == userId);
            Assert.NotNull(notif);
            Assert.Equal("ORDER_STATUS_CHANGED", notif.Type);
        });
    }

    [Fact]
    public async Task CreateOrder_ConcurrentRequestsForSamePackage_OnlyOneSucceeds()
    {
        var (userId, token) = await SeedUserAsync();
        var (originId, destId, _) = await SeedStationsAsync();
        var pkgId = await SeedPackageDirectAsync(userId);

        var tasks = Enumerable.Range(1, 5).Select(async _ =>
        {
            using var req = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
            req.Content = JsonContent.Create(new CreateDeliveryOrderRequest(pkgId, originId, destId, "Concurrent Receiver", "0912345678", null));
            using var res = await _client.SendAsync(req);
            return res.StatusCode;
        });

        var results = await Task.WhenAll(tasks);
        Assert.Single(results, s => s == HttpStatusCode.Created);
        Assert.Equal(4, results.Count(s => s == HttpStatusCode.Conflict));
    }
}
