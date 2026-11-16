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

public class DeliveryConfirmationTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task Owner_completes_arrived_order_once_and_releases_its_slot_without_disclosing_the_code()
    {
        var data = await SeedAsync();
        using var response = await ConfirmAsync(data.Order, data.Owner, new { pickupSecureCode = "593284" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DELIVERED", json.GetProperty("status").GetString());
        Assert.True(json.GetProperty("slotReleased").GetBoolean());
        Assert.Equal(data.Slot, json.GetProperty("slotId").GetGuid());
        Assert.True(json.GetProperty("actualDeliveryTime").GetDateTime() > DateTime.UtcNow.AddMinutes(-1));
        Assert.DoesNotContain("593284", await response.Content.ReadAsStringAsync());
        await factory.WithDbAsync(async db => {
            var order = await db.DeliveryOrders.FindAsync(data.Order);
            Assert.Equal("DELIVERED", order!.Status);
            Assert.NotNull(order.ActualDeliveryTime);
            Assert.Equal("EMPTY", (await db.StationSlots.FindAsync(data.Slot))!.Status);
            var log = Assert.Single(await db.DeliveryStatusLogs.Where(l => l.OrderId == data.Order).ToListAsync());
            Assert.Equal(data.Owner, log.ChangedBy);
            Assert.Equal("DELIVERED", log.ToStatus);
            Assert.DoesNotContain("593284", log.Remarks);
            var notification = Assert.Single(await db.Notifications.Where(n => n.OrderId == data.Order).ToListAsync());
            Assert.DoesNotContain("593284", notification.Message);
            var audit = await db.AuditLogs.Where(a => a.EntityId == data.Order.ToString()).ToListAsync();
            Assert.Single(audit.Where(a => a.Action == "PACKAGE_DELIVERY_CONFIRMATION"));
            Assert.DoesNotContain(audit, a => (a.Reason ?? "").Contains("593284"));
        });
        using var repeated = await ConfirmAsync(data.Order, data.Owner, new { pickupSecureCode = "593284" });
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
    }

    [Fact]
    public async Task Foreign_and_missing_order_are_indistinguishable_and_do_not_consume_attempts()
    {
        var data = await SeedAsync();
        var other = await ActorAsync("CUSTOMER");
        using var foreign = await ConfirmAsync(data.Order, other, new { pickupSecureCode = "000000" });
        using var missing = await ConfirmAsync(Guid.NewGuid(), other, new { pickupSecureCode = "000000" });
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal((await foreign.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString(),
            (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        await factory.WithDbAsync(async db => Assert.Empty(await db.AuditLogs.Where(a => a.EntityId == data.Order.ToString()).ToListAsync()));
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("DISPATCHER")]
    [InlineData("MANAGER")]
    [InlineData("STATION_OPERATOR")]
    public async Task Staff_cannot_use_the_customer_confirmation_route(string role)
    {
        var data = await SeedAsync();
        using var response = await ConfirmAsync(data.Order, await ActorAsync(role), new { pickupSecureCode = "593284" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_confirmation_is_unauthorized()
    {
        var data = await SeedAsync();
        using var response = await ConfirmAsync(data.Order, null, new { pickupSecureCode = "593284" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_customer_token_is_denied_after_deactivation_or_role_change(bool roleChange)
    {
        var data = await SeedAsync();
        var token = Token(data.Owner);
        await factory.WithDbAsync(async db => {
            var user = await db.Users.FindAsync(data.Owner);
            if (roleChange) user!.RoleId = (await db.Roles.SingleAsync(r => r.Code == "ADMIN")).Id;
            else user!.Status = "INACTIVE";
            await db.SaveChangesAsync();
        });
        using var response = await ConfirmWithTokenAsync(data.Order, token, new { pickupSecureCode = "593284" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    public async Task Malformed_code_is_rejected_without_consuming_the_guess_budget(string code)
    {
        var data = await SeedAsync();
        using var response = await ConfirmAsync(data.Order, data.Owner, new { pickupSecureCode = code });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await factory.WithDbAsync(async db => Assert.Empty(await db.AuditLogs.Where(a => a.EntityId == data.Order.ToString()).ToListAsync()));
    }

    [Fact]
    public async Task Third_wrong_guess_locks_correct_code_and_shares_attempts_with_operator_validation()
    {
        var data = await SeedAsync();
        using var first = await ConfirmAsync(data.Order, data.Owner, new { pickupSecureCode = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal(2, (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("attemptsRemaining").GetInt32());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(data.Operator));
        using var second = await client.PostAsJsonAsync($"/api/landing-stations/{data.Station}/operations/validate-code",
            new { trackingNumber = data.Tracking, pickupSecureCode = "000000" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("attemptsRemaining").GetInt32());
        using var third = await ConfirmAsync(data.Order, data.Owner, new { pickupSecureCode = "000000" });
        Assert.Equal((HttpStatusCode)423, third.StatusCode);
        Assert.True(int.Parse(third.Headers.GetValues("Retry-After").Single()) > 0);
        Assert.True((await third.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lockoutExpiresAt").GetDateTime() > DateTime.UtcNow);
        using var correctWhileLocked = await ConfirmAsync(data.Order, data.Owner, new { pickupSecureCode = "593284" });
        Assert.Equal((HttpStatusCode)423, correctWhileLocked.StatusCode);
        await factory.WithDbAsync(async db => {
            Assert.Equal("ARRIVED", (await db.DeliveryOrders.FindAsync(data.Order))!.Status);
            Assert.Equal("OCCUPIED", (await db.StationSlots.FindAsync(data.Slot))!.Status);
            Assert.Equal(3, await db.AuditLogs.CountAsync(a => a.EntityId == data.Order.ToString() && a.Action == "PICKUP_OTP_FAILED"));
        });
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("IN_TRANSIT")]
    [InlineData("FAILED")]
    [InlineData("CANCELLED")]
    public async Task Only_arrived_orders_can_be_completed(string status)
    {
        var data = await SeedAsync();
        await factory.WithDbAsync(async db => { (await db.DeliveryOrders.FindAsync(data.Order))!.Status = status; await db.SaveChangesAsync(); });
        using var response = await ConfirmAsync(data.Order, data.Owner, new { pickupSecureCode = "593284" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Unoccupied_destination_slot_prevents_completion()
    {
        var data = await SeedAsync();
        await factory.WithDbAsync(async db => { (await db.StationSlots.FindAsync(data.Slot))!.Status = "EMPTY"; await db.SaveChangesAsync(); });
        using var response = await ConfirmAsync(data.Order, data.Owner, new { pickupSecureCode = "593284" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await factory.WithDbAsync(async db => Assert.Equal("ARRIVED", (await db.DeliveryOrders.FindAsync(data.Order))!.Status));
    }

    [Fact]
    public async Task Client_cannot_override_tracking_station_or_status_in_request_body()
    {
        var data = await SeedAsync();
        using var response = await ConfirmAsync(data.Order, data.Owner,
            new { pickupSecureCode = "593284", trackingNumber = "another-order", status = "DELIVERED" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    internal async Task<(Guid Order, Guid Owner, Guid Operator, Guid Station, Guid Slot, string Tracking)> SeedAsync()
    {
        var stationOperator = await ActorAsync("STATION_OPERATOR");
        (Guid Order, Guid Owner, Guid Operator, Guid Station, Guid Slot, string Tracking) result = default;
        await factory.WithDbAsync(async db => {
            var missionId = await TelemetryTestData.SeedMissionAsync(db, "COMPLETED");
            var mission = await db.FlightMissions.Include(m => m.Order).ThenInclude(o => o.DestinationStation).SingleAsync(m => m.Id == missionId);
            mission.Order.Status = "ARRIVED";
            mission.Order.DestinationStation.OperatorId = stationOperator;
            var slot = new StationSlot { StationId = mission.Order.DestinationStationId, SlotNumber = 1, Status = "OCCUPIED" };
            db.StationSlots.Add(slot);
            mission.DestinationSlot = slot;
            await db.SaveChangesAsync();
            result = (mission.OrderId, mission.Order.CustomerId, stationOperator, slot.StationId, slot.Id, mission.Order.TrackingNumber);
        });
        return result;
    }

    internal async Task<Guid> ActorAsync(string role)
    {
        var id = Guid.NewGuid();
        await factory.WithDbAsync(async db => {
            db.Users.Add(new User { Id = id, RoleId = (await db.Roles.SingleAsync(r => r.Code == role)).Id,
                Email = $"confirmation-{id:N}@example.com", Status = "ACTIVE" });
            await db.SaveChangesAsync();
        });
        return id;
    }

    internal string Token(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(
            db.Users.Include(u => u.Role).Single(u => u.Id == userId), DateTime.UtcNow);
    }

    private Task<HttpResponseMessage> ConfirmAsync(Guid order, Guid? user, object body) => ConfirmWithTokenAsync(order, user is { } id ? Token(id) : null, body);
    private async Task<HttpResponseMessage> ConfirmWithTokenAsync(Guid order, string? token, object body)
    {
        using var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.PostAsJsonAsync($"/api/delivery-orders/{order}/confirm-delivery", body);
    }
}
