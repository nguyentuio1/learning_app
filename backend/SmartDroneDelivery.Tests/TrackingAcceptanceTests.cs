using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Tests;

public class TrackingAcceptanceTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("12345")]
    public async Task Invalid_order_uuid_returns_bad_request(string invalidId)
    {
        var helper = new TrackingApiTests(factory);
        var customer = await helper.ActorAsync("CUSTOMER");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", helper.Token(customer));

        using var response = await client.GetAsync($"/api/delivery-orders/{invalidId}/tracking");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Snapshot_includes_origin_destination_coordinates_and_utc_eta()
    {
        var helper = new TrackingApiTests(factory);
        var data = await helper.SeedAsync("IN_AIR", "IN_TRANSIT", sample: true);
        var eta = DateTime.UtcNow.AddMinutes(18);

        await factory.WithDbAsync(async db =>
        {
            var order = await db.DeliveryOrders
                .Include(o => o.OriginStation)
                .Include(o => o.DestinationStation)
                .SingleAsync(o => o.Id == data.OrderId);
            order.EstimatedDeliveryTime = eta;
            order.OriginStation.Latitude = 10.7769m;
            order.OriginStation.Longitude = 106.7009m;
            order.DestinationStation.Latitude = 10.8231m;
            order.DestinationStation.Longitude = 106.6297m;
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", helper.Token(data.Owner));
        using var response = await client.GetAsync($"/api/delivery-orders/{data.OrderId}/tracking");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(10.7769m, json.GetProperty("originStation").GetProperty("latitude").GetDecimal());
        Assert.Equal(106.7009m, json.GetProperty("originStation").GetProperty("longitude").GetDecimal());
        Assert.Equal(10.8231m, json.GetProperty("destinationStation").GetProperty("latitude").GetDecimal());
        Assert.Equal(106.6297m, json.GetProperty("destinationStation").GetProperty("longitude").GetDecimal());
        Assert.Equal(DateTimeKind.Utc, json.GetProperty("estimatedDeliveryTime").GetDateTime().Kind);
        Assert.Equal(eta, json.GetProperty("estimatedDeliveryTime").GetDateTime());
    }

    [Fact]
    public async Task Telemetry_older_than_60_seconds_marks_snapshot_stale_while_keeping_latest_position()
    {
        var helper = new TrackingApiTests(factory);
        var data = await helper.SeedAsync("IN_AIR", "IN_TRANSIT", sample: false);

        await factory.WithDbAsync(async db =>
        {
            db.FlightTelemetryLogs.AddRange(
                new FlightTelemetryLog
                {
                    MissionId = data.MissionId,
                    Latitude = 10.10m,
                    Longitude = 106.10m,
                    AltitudeMeters = 60,
                    SpeedMps = 10,
                    BatteryPercentage = 90,
                    Timestamp = DateTime.UtcNow.AddSeconds(-120)
                },
                new FlightTelemetryLog
                {
                    MissionId = data.MissionId,
                    Latitude = 10.35m,
                    Longitude = 106.55m,
                    AltitudeMeters = 75,
                    SpeedMps = 11,
                    BatteryPercentage = 88,
                    Timestamp = DateTime.UtcNow.AddSeconds(-65)
                });
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", helper.Token(data.Owner));
        using var response = await client.GetAsync($"/api/delivery-orders/{data.OrderId}/tracking");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("telemetryStale").GetBoolean());
        Assert.Equal(JsonValueKind.Object, json.GetProperty("lastPosition").ValueKind);
        Assert.Equal(10.35m, json.GetProperty("lastPosition").GetProperty("latitude").GetDecimal());
        Assert.Equal(88, json.GetProperty("lastPosition").GetProperty("batteryPercentage").GetInt32());
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("APPROVED")]
    [InlineData("READY_FOR_TAKEOFF")]
    [InlineData("DELIVERED")]
    [InlineData("CANCELLED")]
    [InlineData("FAILED")]
    public async Task Non_in_transit_orders_hide_last_position_and_are_not_marked_stale(string orderStatus)
    {
        var helper = new TrackingApiTests(factory);
        var data = await helper.SeedAsync("IN_AIR", orderStatus, sample: true);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", helper.Token(data.Owner));
        using var response = await client.GetAsync($"/api/delivery-orders/{data.OrderId}/tracking");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderStatus, json.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("lastPosition").ValueKind);
        Assert.False(json.GetProperty("telemetryStale").GetBoolean());
    }
}
