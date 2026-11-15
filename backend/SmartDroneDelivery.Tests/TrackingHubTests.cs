using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Tracking;

namespace SmartDroneDelivery.Tests;

public class TrackingHubTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task Switching_orders_keeps_one_selection_and_disconnect_removes_it()
    {
        var fixture = new TrackingApiTests(factory);
        var first = await fixture.SeedAsync();
        var second = await fixture.SeedAsync();
        var staff = await fixture.ActorAsync("ADMIN");
        var registry = factory.Services.GetRequiredService<TrackingSubscriptions>();
        using var socket = await TrackingSocket.ConnectAsync(factory.Server.CreateWebSocketClient(), fixture.Token(staff));
        Assert.False((await socket.InvokeAsync("Subscribe", first.OrderId.ToString())).TryGetProperty("error", out _));
        Assert.Equal(first.OrderId, Assert.Single(registry.Connections.Where(c => c.UserId == staff)).OrderId);
        Assert.False((await socket.InvokeAsync("Subscribe", second.OrderId.ToString())).TryGetProperty("error", out _));
        Assert.Equal(second.OrderId, Assert.Single(registry.Connections.Where(c => c.UserId == staff)).OrderId);
        socket.Dispose();
        for (var attempt = 0; attempt < 100 && registry.Connections.Any(c => c.UserId == staff); attempt++)
            await Task.Delay(10);
        Assert.DoesNotContain(registry.Connections, c => c.UserId == staff);
    }

    [Fact]
    public async Task Committed_order_change_is_pushed_without_a_client_refresh()
    {
        var fixture = new TrackingApiTests(factory);
        var data = await fixture.SeedAsync();
        using var socket = await TrackingSocket.ConnectAsync(factory.Server.CreateWebSocketClient(), fixture.Token(data.Owner));
        await socket.InvokeAsync("Subscribe", data.OrderId.ToString());
        await factory.WithDbAsync(async db => {
            var order = await db.DeliveryOrders.FindAsync(data.OrderId);
            order!.Status = "ARRIVED";
            order.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });
        var update = await socket.ReadUntilAsync(frame => IsEvent(frame, "StatusChanged")
            && frame.GetProperty("arguments")[0].GetProperty("status").GetString() == "ARRIVED");
        Assert.Equal(data.OrderId, update.GetProperty("arguments")[0].GetProperty("orderId").GetGuid());
        Assert.False(update.GetProperty("arguments")[0].TryGetProperty("pickupSecureCode", out _));
    }

    [Fact]
    public async Task Persisted_new_position_is_pushed_and_revoked_account_is_disconnected()
    {
        var fixture = new TrackingApiTests(factory);
        var data = await fixture.SeedAsync(sample: false);
        using var socket = await TrackingSocket.ConnectAsync(factory.Server.CreateWebSocketClient(), fixture.Token(data.Owner));
        await socket.InvokeAsync("Subscribe", data.OrderId.ToString());
        await factory.WithDbAsync(async db => {
            db.FlightTelemetryLogs.Add(new SmartDroneDelivery.Api.Entities.FlightTelemetryLog {
                MissionId = data.MissionId, Latitude = 11.5m, Longitude = 106.5m, SpeedMps = 10,
                AltitudeMeters = 40, BatteryPercentage = 90, Timestamp = DateTime.UtcNow.AddSeconds(-1) });
            await db.SaveChangesAsync();
        });
        var update = await socket.ReadUntilAsync(frame => IsEvent(frame, "PositionUpdated"));
        Assert.Equal(11.5m, update.GetProperty("arguments")[0].GetProperty("latitude").GetDecimal());
        Assert.False(update.GetProperty("arguments")[0].TryGetProperty("orderId", out _));
        await factory.WithDbAsync(async db => {
            (await db.Users.SingleAsync(u => u.Id == data.Owner)).Status = "INACTIVE";
            await db.SaveChangesAsync();
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => socket.ReadUntilAsync(_ => false));
    }

    private static bool IsEvent(JsonElement frame, string name) =>
        frame.TryGetProperty("type", out var type) && type.GetInt32() == 1
        && frame.GetProperty("target").GetString() == name;

    [Fact]
    public async Task Owner_can_connect_subscribe_and_unsubscribe_over_real_signalr_protocol()
    {
        var fixture = new TrackingApiTests(factory);
        var data = await fixture.SeedAsync();
        using var socket = await TrackingSocket.ConnectAsync(factory.Server.CreateWebSocketClient(), fixture.Token(data.Owner));
        var subscribed = await socket.InvokeAsync("Subscribe", data.OrderId.ToString());
        Assert.False(subscribed.TryGetProperty("error", out _));
        var unsubscribed = await socket.InvokeAsync("Unsubscribe");
        Assert.False(unsubscribed.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task Foreign_and_unknown_order_subscriptions_fail_without_disclosing_order_existence()
    {
        var fixture = new TrackingApiTests(factory);
        var data = await fixture.SeedAsync();
        var other = await fixture.ActorAsync("CUSTOMER");
        using var socket = await TrackingSocket.ConnectAsync(factory.Server.CreateWebSocketClient(), fixture.Token(other));
        var foreign = await socket.InvokeAsync("Subscribe", data.OrderId.ToString());
        var missing = await socket.InvokeAsync("Subscribe", Guid.NewGuid().ToString());
        Assert.True(foreign.TryGetProperty("error", out var denied));
        Assert.Equal(denied.GetString(), missing.GetProperty("error").GetString());
        Assert.DoesNotContain(data.OrderId.ToString(), denied.GetString());
    }

    [Fact]
    public async Task Anonymous_hub_negotiate_is_unauthorized_and_query_token_is_scoped_to_hub()
    {
        var fixture = new TrackingApiTests(factory);
        var data = await fixture.SeedAsync();
        using var client = factory.CreateClient();
        using var negotiate = await client.PostAsync("/hubs/tracking/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, negotiate.StatusCode);
        using var queryToken = await client.GetAsync($"/api/delivery-orders/{data.OrderId}/tracking?access_token={fixture.Token(data.Owner)}");
        Assert.Equal(HttpStatusCode.Unauthorized, queryToken.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(data.Owner));
        using var authenticated = await client.PostAsync("/hubs/tracking/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
    }
}

internal sealed class TrackingSocket(WebSocket socket) : IDisposable
{
    private readonly Queue<JsonElement> _frames = new();
    private int _invocation;

    public static async Task<TrackingSocket> ConnectAsync(WebSocketClient client, string token)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var connection = new TrackingSocket(await client.ConnectAsync(
            new Uri($"ws://localhost/hubs/tracking?access_token={token}"), timeout.Token));
        await connection.SendAsync(new { protocol = "json", version = 1 }, timeout.Token);
        var handshake = await connection.ReadUntilAsync(frame => !frame.TryGetProperty("type", out _));
        Assert.False(handshake.TryGetProperty("error", out _));
        return connection;
    }

    public async Task<JsonElement> InvokeAsync(string target, params object[] arguments)
    {
        var id = (++_invocation).ToString();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await SendAsync(new { type = 1, invocationId = id, target, arguments }, timeout.Token);
        return await ReadUntilAsync(frame => frame.TryGetProperty("type", out var type) && type.GetInt32() == 3
            && frame.GetProperty("invocationId").GetString() == id);
    }

    public async Task<JsonElement> ReadUntilAsync(Func<JsonElement, bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            while (_frames.TryDequeue(out var frame)) if (predicate(frame)) return frame;
            using var message = new MemoryStream();
            var bytes = new byte[4096];
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(bytes), timeout.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    throw new InvalidOperationException("Tracking socket closed before the expected frame.");
                message.Write(bytes, 0, result.Count);
            } while (!result.EndOfMessage);
            foreach (var json in Encoding.UTF8.GetString(message.ToArray()).Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
            {
                using var document = JsonDocument.Parse(json);
                _frames.Enqueue(document.RootElement.Clone());
            }
        }
    }

    private Task SendAsync(object body, CancellationToken ct) => socket.SendAsync(
        new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body) + "\u001e")),
        WebSocketMessageType.Text, true, ct);

    public void Dispose() => socket.Dispose();
}
