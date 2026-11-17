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

public class DeliveryEtaReadTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly DateTime PredictionTime = new(2026, 11, 16, 7, 55, 0, DateTimeKind.Utc);

    private async Task<(Guid Id, string Token)> UserAsync(string role = AppRoles.Customer)
    {
        var id = Guid.NewGuid();
        string token = "";
        await factory.WithDbAsync(async db =>
        {
            var roleRow = await db.Roles.SingleAsync(r => r.Code == role);
            var user = new User { Id = id, Email = $"eta-{id:N}@example.com", FullName = "ETA reader",
                PasswordHash = "unused", PhoneNumber = "0901234567", Role = roleRow, RoleId = roleRow.Id, Status = "ACTIVE" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            using var scope = factory.Services.CreateScope();
            token = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, DateTime.UtcNow);
        });
        return (id, token);
    }

    private async Task<Guid> OrderAsync(Guid owner, bool prediction = false)
    {
        var id = Guid.NewGuid();
        await factory.WithDbAsync(async db =>
        {
            var origin = new LandingStation { StationCode = $"E-{Guid.NewGuid():N}", Name = "Origin", Address = "A" };
            var destination = new LandingStation { StationCode = $"E-{Guid.NewGuid():N}", Name = "Destination", Address = "B" };
            var package = new Package { SenderId = owner, PackageName = "Parcel", WeightGram = 100 };
            db.LandingStations.AddRange(origin, destination);
            db.Packages.Add(package);
            db.DeliveryOrders.Add(new DeliveryOrder { Id = id, CustomerId = owner, PackageId = package.Id,
                OriginStationId = origin.Id, DestinationStationId = destination.Id, TrackingNumber = $"ETA-{id:N}",
                RecipientName = "Private recipient", RecipientPhone = "0909999999", PickupSecureCode = "654321",
                Status = "IN_TRANSIT", EstimatedDeliveryTime = PredictionTime.AddMinutes(29) });
            if (prediction)
                db.AiEtaPredictions.Add(new AiEtaPrediction { OrderId = id, CalculatedDistanceKm = 12.35m,
                    PredictedDurationMinutes = 18, CreatedAt = PredictionTime });
            await db.SaveChangesAsync();
        });
        return id;
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string? token)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Owner_gets_stored_estimate_without_inventing_prediction_or_writing()
    {
        var owner = await UserAsync();
        var order = await OrderAsync(owner.Id);
        using var response = await GetAsync($"/api/delivery-orders/{order}/eta", owner.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, body.EnumerateObject().Count());
        Assert.Equal(order, body.GetProperty("orderId").GetGuid());
        Assert.Equal("IN_TRANSIT", body.GetProperty("status").GetString());
        Assert.Equal(PredictionTime.AddMinutes(29), body.GetProperty("estimatedDeliveryTime").GetDateTime());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("actualDeliveryTime").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("aiEtaPrediction").ValueKind);
        await factory.WithDbAsync(async db => Assert.False(await db.AiEtaPredictions.AnyAsync(p => p.OrderId == order)));
    }

    [Fact]
    public async Task Detail_and_dedicated_route_return_same_stored_prediction_without_refreshing_it()
    {
        var owner = await UserAsync();
        var order = await OrderAsync(owner.Id, true);
        using var eta = await GetAsync($"/api/delivery-orders/{order}/eta", owner.Token);
        using var detail = await GetAsync($"/api/delivery-orders/{order}", owner.Token);
        Assert.Equal(HttpStatusCode.OK, eta.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.True(detail.Headers.CacheControl?.NoStore);
        var a = (await eta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("aiEtaPrediction");
        var b = (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("aiEtaPrediction");
        Assert.Equal(JsonValueKind.Object, a.ValueKind);
        Assert.Equal(a.GetRawText(), b.GetRawText());
        Assert.Equal(5, a.EnumerateObject().Count());
        Assert.Equal(12.35m, a.GetProperty("calculatedDistanceKm").GetDecimal());
        Assert.Equal(18, a.GetProperty("predictedDurationMinutes").GetInt32());
        Assert.Equal(PredictionTime, a.GetProperty("createdAt").GetDateTime());
        Assert.EndsWith("Z", a.GetProperty("createdAt").GetString());
        Assert.Equal(JsonValueKind.Null, a.GetProperty("weatherCondition").ValueKind);
        Assert.Equal(JsonValueKind.Null, a.GetProperty("confidenceScore").ValueKind);
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.AiEtaPredictions.SingleAsync(p => p.OrderId == order);
            Assert.Equal(PredictionTime, stored.CreatedAt);
            Assert.Equal(18, stored.PredictedDurationMinutes);
        });
    }

    [Fact]
    public async Task Unknown_and_foreign_orders_are_indistinguishable_on_both_routes()
    {
        var owner = await UserAsync();
        var stranger = await UserAsync();
        var order = await OrderAsync(owner.Id, true);
        foreach (var suffix in new[] { "", "/eta" })
        {
            using var foreign = await GetAsync($"/api/delivery-orders/{order}{suffix}", stranger.Token);
            using var missing = await GetAsync($"/api/delivery-orders/{Guid.NewGuid()}{suffix}", stranger.Token);
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            var a = await foreign.Content.ReadFromJsonAsync<JsonElement>();
            var b = await missing.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(a.GetProperty("detail").GetString(), b.GetProperty("detail").GetString());
        }
    }

    [Theory]
    [InlineData(AppRoles.Dispatcher, HttpStatusCode.OK)]
    [InlineData(AppRoles.Manager, HttpStatusCode.OK)]
    [InlineData(AppRoles.Admin, HttpStatusCode.Forbidden)]
    [InlineData(AppRoles.StationOperator, HttpStatusCode.Forbidden)]
    public async Task Staff_read_policy_does_not_open_customer_detail(string role, HttpStatusCode expected)
    {
        var owner = await UserAsync();
        var staff = await UserAsync(role);
        var order = await OrderAsync(owner.Id, true);
        using var eta = await GetAsync($"/api/delivery-orders/{order}/eta", staff.Token);
        Assert.Equal(expected, eta.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var body = await eta.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(5, body.EnumerateObject().Count());
            Assert.False(body.TryGetProperty("recipientPhone", out _));
            Assert.DoesNotContain("654321", body.GetRawText());
        }
        using var detail = await GetAsync($"/api/delivery-orders/{order}", staff.Token);
        Assert.Equal(HttpStatusCode.Forbidden, detail.StatusCode);
    }

    [Theory]
    [InlineData("INACTIVE", null)]
    [InlineData("ACTIVE", AppRoles.Admin)]
    [InlineData("ACTIVE", AppRoles.Dispatcher)]
    public async Task Detail_rechecks_persisted_customer_role_and_status(string status, string? role)
    {
        var owner = await UserAsync();
        var order = await OrderAsync(owner.Id, true);
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == owner.Id);
            user.Status = status;
            if (role is not null) user.RoleId = (await db.Roles.SingleAsync(r => r.Code == role)).Id;
            await db.SaveChangesAsync();
        });
        using var response = await GetAsync($"/api/delivery-orders/{order}", owner.Token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var eta = await GetAsync($"/api/delivery-orders/{order}/eta", owner.Token);
        Assert.Equal(status == "ACTIVE" && role == AppRoles.Dispatcher ? HttpStatusCode.OK : HttpStatusCode.Forbidden, eta.StatusCode);
    }

    [Theory]
    [InlineData(0, 18, null, null)]
    [InlineData(12, 0, null, null)]
    [InlineData(10000, 18, null, null)]
    [InlineData(12, 18, -0.1, null)]
    [InlineData(12, 18, 1.1, null)]
    [InlineData(12, 18, null, "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task Invalid_legacy_prediction_is_omitted_consistently(double distance, int duration, double? confidence, string? weather)
    {
        var owner = await UserAsync();
        var order = await OrderAsync(owner.Id, true);
        await factory.WithDbAsync(async db =>
        {
            var p = await db.AiEtaPredictions.SingleAsync(p => p.OrderId == order);
            p.CalculatedDistanceKm = (decimal)distance;
            p.PredictedDurationMinutes = duration;
            p.ConfidenceScore = confidence.HasValue ? (decimal)confidence.Value : null;
            p.WeatherCondition = weather;
            await db.SaveChangesAsync();
        });
        foreach (var suffix in new[] { "", "/eta" })
        {
            using var response = await GetAsync($"/api/delivery-orders/{order}{suffix}", owner.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Null, body.GetProperty("aiEtaPrediction").ValueKind);
            Assert.Equal(PredictionTime.AddMinutes(29), body.GetProperty("estimatedDeliveryTime").GetDateTime());
        }
    }

    [Fact]
    public async Task Dedicated_route_requires_authentication()
    {
        using var response = await GetAsync($"/api/delivery-orders/{Guid.NewGuid()}/eta", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
