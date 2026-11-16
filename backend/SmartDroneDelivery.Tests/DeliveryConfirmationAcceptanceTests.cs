using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class DeliveryConfirmationAcceptanceTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("12345")]
    public async Task Invalid_order_uuid_returns_bad_request(string invalidId)
    {
        var customer = await ActorAsync("CUSTOMER");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(customer));

        using var response = await client.PostAsJsonAsync(
            $"/api/delivery-orders/{invalidId}/confirm-delivery",
            new { pickupSecureCode = "593284" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Expired_order_cannot_be_confirmed_and_returns_conflict()
    {
        var data = await SeedAsync();
        await factory.WithDbAsync(async db =>
        {
            (await db.DeliveryOrders.FindAsync(data.Order))!.Status = "EXPIRED";
            await db.SaveChangesAsync();
        });

        using var response = await ConfirmAsync(data.Order, data.Owner, new { pickupSecureCode = "593284" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal("EXPIRED", (await db.DeliveryOrders.FindAsync(data.Order))!.Status);
            Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(data.Slot))!.Status);
        });
    }

    [Fact]
    public async Task Operator_routes_also_respect_lockout_triggered_by_customer_guesses()
    {
        var data = await SeedAsync();

        for (var i = 0; i < 2; i++)
        {
            using var wrong = await ConfirmAsync(data.Order, data.Owner, new { pickupSecureCode = "000000" });
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        }

        using var third = await ConfirmAsync(data.Order, data.Owner, new { pickupSecureCode = "000000" });
        Assert.Equal((HttpStatusCode)423, third.StatusCode);

        using var opClient = factory.CreateClient();
        opClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(data.Operator));
        using var opValidate = await opClient.PostAsJsonAsync(
            $"/api/landing-stations/{data.Station}/operations/validate-code",
            new { trackingNumber = data.Tracking, pickupSecureCode = "593284" });

        Assert.Equal(HttpStatusCode.OK, opValidate.StatusCode);
        var validateJson = await opValidate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(validateJson.GetProperty("isValid").GetBoolean());
        Assert.True(validateJson.GetProperty("isLocked").GetBoolean());
        Assert.Equal(0, validateJson.GetProperty("attemptsRemaining").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, validateJson.GetProperty("lockoutExpiresAt").ValueKind);

        using var opPickup = await opClient.PostAsJsonAsync(
            $"/api/landing-stations/{data.Station}/operations/pickup",
            new { trackingNumber = data.Tracking, pickupSecureCode = "593284" });
        Assert.Equal((HttpStatusCode)423, opPickup.StatusCode);
    }

    [Fact]
    public async Task Successful_confirmation_records_arrived_to_delivered_log_and_customer_notification()
    {
        var data = await SeedAsync();

        using var response = await ConfirmAsync(data.Order, data.Owner, new { pickupSecureCode = "593284" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await factory.WithDbAsync(async db =>
        {
            var log = await db.DeliveryStatusLogs.SingleAsync(l => l.OrderId == data.Order);
            Assert.Equal("ARRIVED", log.FromStatus);
            Assert.Equal("DELIVERED", log.ToStatus);
            Assert.Equal(data.Owner, log.ChangedBy);

            var notification = await db.Notifications.SingleAsync(n => n.OrderId == data.Order);
            Assert.Equal(data.Owner, notification.UserId);
            Assert.False(notification.IsRead);
        });

        using var opClient = factory.CreateClient();
        opClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(data.Operator));
        using var opPickupAfter = await opClient.PostAsJsonAsync(
            $"/api/landing-stations/{data.Station}/operations/pickup",
            new { trackingNumber = data.Tracking, pickupSecureCode = "593284" });
        Assert.Equal(HttpStatusCode.Conflict, opPickupAfter.StatusCode);
    }

    private async Task<(Guid Order, Guid Owner, Guid Operator, Guid Station, Guid Slot, string Tracking)> SeedAsync()
    {
        var helper = new DeliveryConfirmationTests(factory);
        return await helper.SeedAsync();
    }

    private async Task<Guid> ActorAsync(string role)
    {
        var helper = new DeliveryConfirmationTests(factory);
        return await helper.ActorAsync(role);
    }

    private string Token(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(
            db.Users.Include(u => u.Role).Single(u => u.Id == userId), DateTime.UtcNow);
    }

    private async Task<HttpResponseMessage> ConfirmAsync(Guid order, Guid user, object body)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(user));
        return await client.PostAsJsonAsync($"/api/delivery-orders/{order}/confirm-delivery", body);
    }
}
