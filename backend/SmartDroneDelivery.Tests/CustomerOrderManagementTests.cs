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

public class CustomerOrderManagementTests : IClassFixture<AuthApiFactory>, IDisposable
{
    private const string Password = "Passw0rd123";
    private static readonly string PasswordHash = new PasswordHasher().Hash(Password);
    private static int _counter;
    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public CustomerOrderManagementTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private async Task<(Guid UserId, string Token)> SeedUserAsync(string role = AppRoles.Customer)
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"orderuser{n}@example.com";
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

    private async Task<(Guid OriginId, Guid DestId)> SeedStationsAsync()
    {
        var originId = Guid.NewGuid();
        var destId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            db.LandingStations.Add(new LandingStation
            {
                Id = originId,
                StationCode = $"ST-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                Name = "Origin Station Alpha",
                Address = "123 Alpha Road",
                Status = "OPERATIONAL"
            });

            db.LandingStations.Add(new LandingStation
            {
                Id = destId,
                StationCode = $"ST-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                Name = "Destination Station Beta",
                Address = "456 Beta Boulevard",
                Status = "OPERATIONAL"
            });

            await db.SaveChangesAsync();
        });

        return (originId, destId);
    }

    private HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task CustomerAddress_CrudAndIsolation_Succeeds()
    {
        var (userA, tokenA) = await SeedUserAsync();
        var (userB, tokenB) = await SeedUserAsync();

        // 1. Create first address for User A (should automatically become default)
        var createReq = new CreateCustomerAddressRequest(
            Label: "Home",
            ContactName: "Alice Smith",
            ContactPhone: "0912345678",
            AddressLine: "10 Main St, District 1",
            Latitude: 10.7769m,
            Longitude: 106.7009m,
            IsDefault: false);

        using var postReq = Authorized(HttpMethod.Post, "/api/customer-addresses", tokenA);
        postReq.Content = JsonContent.Create(createReq);
        using var postRes = await _client.SendAsync(postReq);
        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var addrA = await postRes.Content.ReadFromJsonAsync<CustomerAddressDto>();
        Assert.NotNull(addrA);
        Assert.True(addrA.IsDefault);

        // 2. Create second address for User A with IsDefault = true (first should no longer be default)
        var createReq2 = new CreateCustomerAddressRequest(
            Label: "Office",
            ContactName: "Alice Work",
            ContactPhone: "0912345679",
            AddressLine: "20 Work St, District 3",
            Latitude: 10.7800m,
            Longitude: 106.6900m,
            IsDefault: true);

        using var postReq2 = Authorized(HttpMethod.Post, "/api/customer-addresses", tokenA);
        postReq2.Content = JsonContent.Create(createReq2);
        using var postRes2 = await _client.SendAsync(postReq2);
        Assert.Equal(HttpStatusCode.Created, postRes2.StatusCode);
        var addrA2 = await postRes2.Content.ReadFromJsonAsync<CustomerAddressDto>();
        Assert.NotNull(addrA2);
        Assert.True(addrA2.IsDefault);

        // Check list of addresses for User A
        using var listReqA = Authorized(HttpMethod.Get, "/api/customer-addresses", tokenA);
        using var listResA = await _client.SendAsync(listReqA);
        var listA = await listResA.Content.ReadFromJsonAsync<List<CustomerAddressDto>>();
        Assert.NotNull(listA);
        Assert.Equal(2, listA.Count);
        Assert.True(listA.First(a => a.Id == addrA2.Id).IsDefault);
        Assert.False(listA.First(a => a.Id == addrA.Id).IsDefault);

        // 3. User B cannot access User A's address (403 Forbidden)
        using var getBReq = Authorized(HttpMethod.Get, $"/api/customer-addresses/{addrA.Id}", tokenB);
        using var getBRes = await _client.SendAsync(getBReq);
        Assert.Equal(HttpStatusCode.Forbidden, getBRes.StatusCode);

        // 4. Update address
        var updateReq = new UpdateCustomerAddressRequest(
            Label: "Home Sweet Home",
            ContactName: "Alice Smith Updated",
            ContactPhone: "0912345678",
            AddressLine: "10 Main St, District 1, Updated",
            Latitude: 10.7769m,
            Longitude: 106.7009m,
            IsDefault: false);

        using var putReq = Authorized(HttpMethod.Put, $"/api/customer-addresses/{addrA.Id}", tokenA);
        putReq.Content = JsonContent.Create(updateReq);
        using var putRes = await _client.SendAsync(putReq);
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);
        var updated = await putRes.Content.ReadFromJsonAsync<CustomerAddressDto>();
        Assert.Equal("Home Sweet Home", updated!.Label);

        // 5. Delete address
        using var delReq = Authorized(HttpMethod.Delete, $"/api/customer-addresses/{addrA.Id}", tokenA);
        using var delRes = await _client.SendAsync(delReq);
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);
    }

    [Fact]
    public async Task RoleRestrictions_PreventNonCustomers_FromModifyingCustomerResources()
    {
        var (_, dispatcherToken) = await SeedUserAsync(AppRoles.Dispatcher);
        var (originId, destId) = await SeedStationsAsync();

        // 1. Dispatcher cannot create Customer Address (403 Forbidden)
        var createAddr = new CreateCustomerAddressRequest("Addr", "Name", "0900000000", "Street", null, null);
        using var postAddr = Authorized(HttpMethod.Post, "/api/customer-addresses", dispatcherToken);
        postAddr.Content = JsonContent.Create(createAddr);
        using var resAddr = await _client.SendAsync(postAddr);
        Assert.Equal(HttpStatusCode.Forbidden, resAddr.StatusCode);

        // 2. Dispatcher cannot create Package (403 Forbidden)
        var createPkg = new CreatePackageRequest("Box", 500m, 10m, 10m, 10m);
        using var postPkg = Authorized(HttpMethod.Post, "/api/packages", dispatcherToken);
        postPkg.Content = JsonContent.Create(createPkg);
        using var resPkg = await _client.SendAsync(postPkg);
        Assert.Equal(HttpStatusCode.Forbidden, resPkg.StatusCode);

        // 3. Dispatcher cannot create Delivery Order (403 Forbidden)
        var createOrder = new CreateDeliveryOrderRequest(Guid.NewGuid(), originId, destId, "Recipient", "0900000000", null);
        using var postOrder = Authorized(HttpMethod.Post, "/api/delivery-orders", dispatcherToken);
        postOrder.Content = JsonContent.Create(createOrder);
        using var resOrder = await _client.SendAsync(postOrder);
        Assert.Equal(HttpStatusCode.Forbidden, resOrder.StatusCode);
    }

    [Fact]
    public async Task Package_ValidationAndImageUpload_Succeeds()
    {
        var (userId, token) = await SeedUserAsync();
        var (_, otherToken) = await SeedUserAsync();

        // 1. Invalid weight (> 5000g) rejected with 400
        var invalidReq = new CreatePackageRequest("Too heavy package", 6000m, 10m, 10m, 10m);
        using var postBad = Authorized(HttpMethod.Post, "/api/packages", token);
        postBad.Content = JsonContent.Create(invalidReq);
        using var resBad = await _client.SendAsync(postBad);
        Assert.Equal(HttpStatusCode.BadRequest, resBad.StatusCode);

        // 2. Invalid dimensions (> 100cm) rejected with 400
        var invalidDim = new CreatePackageRequest("Too long package", 500m, 150m, 10m, 10m);
        using var postBadDim = Authorized(HttpMethod.Post, "/api/packages", token);
        postBadDim.Content = JsonContent.Create(invalidDim);
        using var resBadDim = await _client.SendAsync(postBadDim);
        Assert.Equal(HttpStatusCode.BadRequest, resBadDim.StatusCode);

        // 3. Valid package creation
        var validReq = new CreatePackageRequest("Medical Supplies", 1200m, 25m, 15m, 10m, IsFragile: true);
        using var postGood = Authorized(HttpMethod.Post, "/api/packages", token);
        postGood.Content = JsonContent.Create(validReq);
        using var resGood = await _client.SendAsync(postGood);
        Assert.Equal(HttpStatusCode.Created, resGood.StatusCode);
        var package = await resGood.Content.ReadFromJsonAsync<PackageDto>();
        Assert.NotNull(package);
        Assert.Equal("Medical Supplies", package.PackageName);
        Assert.True(package.IsFragile);

        // 4. Upload package image
        using var imageForm = new MultipartFormDataContent();
        var fileBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // fake jpeg header
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        imageForm.Add(fileContent, "file", "package.jpg");

        using var imgReq = Authorized(HttpMethod.Post, $"/api/packages/{package.Id}/image", token);
        imgReq.Content = imageForm;
        using var imgRes = await _client.SendAsync(imgReq);
        Assert.Equal(HttpStatusCode.OK, imgRes.StatusCode);
        var imgDoc = await imgRes.Content.ReadFromJsonAsync<PackageImageResponse>();
        Assert.NotNull(imgDoc);
        Assert.Contains($"/packages/{package.Id}.jpg", imgDoc.ImageUrl);

        // 5. Another customer cannot see or modify this package
        using var getOther = Authorized(HttpMethod.Get, $"/api/packages/{package.Id}", otherToken);
        using var resOther = await _client.SendAsync(getOther);
        Assert.Equal(HttpStatusCode.Forbidden, resOther.StatusCode);
    }

    [Fact]
    public async Task DeliveryOrder_Creation_Update_Cancel_Flow_WithNotificationsAndDateFilter_Succeeds()
    {
        var (userId, token) = await SeedUserAsync();
        var (otherUserId, otherToken) = await SeedUserAsync();
        var (originId, destId) = await SeedStationsAsync();

        // 1. Create a package
        var pkgReq = new CreatePackageRequest("Electronics Kit", 800m, 20m, 15m, 5m, false);
        using var postPkg = Authorized(HttpMethod.Post, "/api/packages", token);
        postPkg.Content = JsonContent.Create(pkgReq);
        using var resPkg = await _client.SendAsync(postPkg);
        var pkg = await resPkg.Content.ReadFromJsonAsync<PackageDto>();
        Assert.NotNull(pkg);

        // 2. Cannot create order if origin == destination
        var sameStationReq = new CreateDeliveryOrderRequest(pkg.Id, originId, originId, "John Doe", "0987654321", null);
        using var postSame = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        postSame.Content = JsonContent.Create(sameStationReq);
        using var resSame = await _client.SendAsync(postSame);
        Assert.Equal(HttpStatusCode.BadRequest, resSame.StatusCode);

        // 3. User B cannot create an order using User A's package
        var stealPkgReq = new CreateDeliveryOrderRequest(pkg.Id, originId, destId, "Intruder", "0987654321", null);
        using var postSteal = Authorized(HttpMethod.Post, "/api/delivery-orders", otherToken);
        postSteal.Content = JsonContent.Create(stealPkgReq);
        using var resSteal = await _client.SendAsync(postSteal);
        Assert.Equal(HttpStatusCode.Forbidden, resSteal.StatusCode);

        // 4. Create delivery order successfully
        var orderReq = new CreateDeliveryOrderRequest(pkg.Id, originId, destId, "John Recipient", "0987654321", null);
        using var postOrder = Authorized(HttpMethod.Post, "/api/delivery-orders", token);
        postOrder.Content = JsonContent.Create(orderReq);
        using var resOrder = await _client.SendAsync(postOrder);
        Assert.Equal(HttpStatusCode.Created, resOrder.StatusCode);
        var order = await resOrder.Content.ReadFromJsonAsync<DeliveryOrderDetailDto>();
        Assert.NotNull(order);
        Assert.Equal("PENDING", order.Status);
        Assert.StartsWith("DD-", order.TrackingNumber);
        Assert.Equal(6, order.PickupSecureCode.Length);
        Assert.Single(order.StatusLogs);
        Assert.Equal("PENDING", order.StatusLogs[0].ToStatus);

        // FR-12: Check that notification was created for Customer on Order Created
        await _factory.WithDbAsync(async db =>
        {
            var notif = await db.Notifications.FirstOrDefaultAsync(n => n.OrderId == order.Id && n.UserId == userId);
            Assert.NotNull(notif);
            Assert.Equal("ORDER_STATUS_CHANGED", notif.Type);
            Assert.Equal("Order Created", notif.Title);
            Assert.Contains(order.TrackingNumber, notif.Message);
        });

        // 5. Update order while in PENDING
        var updateReq = new UpdateDeliveryOrderRequest("John Recipient Updated", "0987111222", null);
        using var putOrder = Authorized(HttpMethod.Put, $"/api/delivery-orders/{order.Id}", token);
        putOrder.Content = JsonContent.Create(updateReq);
        using var resUpdate = await _client.SendAsync(putOrder);
        Assert.Equal(HttpStatusCode.OK, resUpdate.StatusCode);
        var updatedOrder = await resUpdate.Content.ReadFromJsonAsync<DeliveryOrderDetailDto>();
        Assert.Equal("John Recipient Updated", updatedOrder!.RecipientName);

        // 6. User B cannot view order
        using var getOther = Authorized(HttpMethod.Get, $"/api/delivery-orders/{order.Id}", otherToken);
        using var resOther = await _client.SendAsync(getOther);
        Assert.Equal(HttpStatusCode.Forbidden, resOther.StatusCode);

        // 7. Cancel order with reason
        var cancelReq = new CancelDeliveryOrderRequest("Change of delivery destination");
        using var postCancel = Authorized(HttpMethod.Post, $"/api/delivery-orders/{order.Id}/cancel", token);
        postCancel.Content = JsonContent.Create(cancelReq);
        using var resCancel = await _client.SendAsync(postCancel);
        Assert.Equal(HttpStatusCode.OK, resCancel.StatusCode);
        var cancelledOrder = await resCancel.Content.ReadFromJsonAsync<DeliveryOrderDetailDto>();
        Assert.Equal("CANCELLED", cancelledOrder!.Status);
        Assert.Equal("Change of delivery destination", cancelledOrder.CancelReason);
        Assert.Equal(2, cancelledOrder.StatusLogs.Count);
        Assert.Equal("CANCELLED", cancelledOrder.StatusLogs.Last().ToStatus);

        // FR-12: Check that notification was created for Customer on Order Cancelled
        await _factory.WithDbAsync(async db =>
        {
            var cancelNotifs = await db.Notifications
                .Where(n => n.OrderId == order.Id && n.UserId == userId)
                .OrderBy(n => n.CreatedAt)
                .ToListAsync();
            Assert.Equal(2, cancelNotifs.Count);
            Assert.Equal("Order Cancelled", cancelNotifs.Last().Title);
            Assert.Contains("Change of delivery destination", cancelNotifs.Last().Message);
        });

        // 8. Cannot update cancelled order
        using var putAfterCancel = Authorized(HttpMethod.Put, $"/api/delivery-orders/{order.Id}", token);
        putAfterCancel.Content = JsonContent.Create(updateReq);
        using var resAfterCancel = await _client.SendAsync(putAfterCancel);
        Assert.Equal(HttpStatusCode.Conflict, resAfterCancel.StatusCode);

        // 9. Status history endpoint
        using var histReq = Authorized(HttpMethod.Get, $"/api/delivery-orders/{order.Id}/status-history", token);
        using var histRes = await _client.SendAsync(histReq);
        Assert.Equal(HttpStatusCode.OK, histRes.StatusCode);
        var history = await histRes.Content.ReadFromJsonAsync<List<DeliveryStatusLogDto>>();
        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
        Assert.Equal("PENDING", history[0].ToStatus);
        Assert.Equal("CANCELLED", history[1].ToStatus);

        // 10. List orders with filter by status, pagination, and date range
        var fromStr = DateTime.UtcNow.AddMinutes(-5).ToString("O");
        var toStr = DateTime.UtcNow.AddMinutes(5).ToString("O");
        using var listReq = Authorized(HttpMethod.Get, $"/api/delivery-orders?status=CANCELLED&fromDate={fromStr}&toDate={toStr}&page=1&pageSize=10", token);
        using var listRes = await _client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var paged = await listRes.Content.ReadFromJsonAsync<PagedResult<DeliveryOrderSummaryDto>>();
        Assert.NotNull(paged);
        Assert.Single(paged.Items);
        Assert.Equal(order.Id, paged.Items[0].Id);

        // Date range in future returns 0 orders
        var futureFrom = DateTime.UtcNow.AddDays(1).ToString("O");
        using var listFutureReq = Authorized(HttpMethod.Get, $"/api/delivery-orders?fromDate={futureFrom}&page=1&pageSize=10", token);
        using var listFutureRes = await _client.SendAsync(listFutureReq);
        var futurePaged = await listFutureRes.Content.ReadFromJsonAsync<PagedResult<DeliveryOrderSummaryDto>>();
        Assert.NotNull(futurePaged);
        Assert.Empty(futurePaged.Items);
    }
}
