using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;
using SmartDroneDelivery.Api.Tracking;

namespace SmartDroneDelivery.Tests;

public class TrackingBroadcasterTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task Concurrent_publication_skips_a_connection_with_an_in_progress_send()
    {
        var fixture = new TrackingApiTests(factory);
        var data = await fixture.SeedAsync();
        var (publisher, subscriptions, transport, clock) = Setup();
        var connection = new TrackingConnection("viewer", data.Owner, clock.Now.AddMinutes(10), () => { });
        connection.Select(data.OrderId);
        subscriptions.Add(connection);
        transport.BlockConnection = "viewer";
        var first = publisher.PublishAsync();
        try {
            await transport.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await publisher.PublishAsync();
            Assert.Empty(transport.Events);
        }
        finally { transport.ReleaseSend.TrySetResult(); }
        await first;
        Assert.Equal(2, transport.Events.Count);
    }

    [Fact]
    public async Task Replacement_mission_can_publish_an_older_timestamp_without_reusing_old_flight_cache()
    {
        var fixture = new TrackingApiTests(factory);
        var data = await fixture.SeedAsync();
        var (publisher, subscriptions, transport, clock) = Setup();
        var connection = new TrackingConnection("viewer", data.Owner, clock.Now.AddMinutes(10), () => { });
        connection.Select(data.OrderId);
        subscriptions.Add(connection);
        await publisher.PublishAsync();
        await factory.WithDbAsync(async db => {
            var old = await db.FlightMissions.SingleAsync(m => m.Id == data.MissionId);
            old.FlightStatus = "ABORTED";
            var current = new FlightMission { OrderId = data.OrderId, DroneId = old.DroneId,
                FlightStatus = "IN_AIR", CreatedAt = old.CreatedAt.AddMinutes(1) };
            db.FlightMissions.Add(current);
            db.FlightTelemetryLogs.Add(new FlightTelemetryLog { Mission = current, Latitude = 12,
                Longitude = 106, Timestamp = connection.LastPosition!.Timestamp.AddSeconds(-1) });
            await db.SaveChangesAsync();
        });
        await publisher.PublishAsync();
        var positions = transport.Events.Where(e => e.Method == "PositionUpdated").ToArray();
        Assert.Equal(2, positions.Length);
        Assert.Equal(12m, Assert.IsType<TelemetryPosition>(positions[1].Args[0]).Latitude);
    }

    [Fact]
    public async Task Landing_pushes_status_without_presenting_final_sample_as_a_live_position()
    {
        var fixture = new TrackingApiTests(factory);
        var data = await fixture.SeedAsync();
        var (publisher, subscriptions, transport, clock) = Setup();
        var connection = new TrackingConnection("viewer", data.Owner, clock.Now.AddMinutes(10), () => { });
        connection.Select(data.OrderId);
        subscriptions.Add(connection);
        await publisher.PublishAsync();
        await factory.WithDbAsync(async db => {
            (await db.FlightMissions.FindAsync(data.MissionId))!.FlightStatus = "COMPLETED";
            (await db.DeliveryOrders.FindAsync(data.OrderId))!.Status = "ARRIVED";
            await db.SaveChangesAsync();
        });
        await publisher.PublishAsync();
        Assert.Single(transport.Events.Where(e => e.Method == "PositionUpdated"));
        Assert.Equal("ARRIVED", Assert.IsType<OrderDecisionDto>(transport.Events.Last().Args[0]).Status);
        Assert.Null(connection.LastPosition);
    }

    [Fact]
    public async Task Unchanged_and_late_samples_are_not_republished_and_new_flight_clears_old_position()
    {
        var fixture = new TrackingApiTests(factory);
        var data = await fixture.SeedAsync();
        var (publisher, subscriptions, transport, clock) = Setup();
        var connection = new TrackingConnection("viewer", data.Owner, clock.GetUtcNow().AddMinutes(10), () => { });
        connection.Select(data.OrderId);
        subscriptions.Add(connection);
        await publisher.PublishAsync();
        Assert.Single(transport.Events.Where(e => e.Method == "PositionUpdated"));
        await publisher.PublishAsync();
        Assert.Equal(2, transport.Events.Count);
        await factory.WithDbAsync(async db => {
            db.FlightTelemetryLogs.Add(new FlightTelemetryLog { MissionId = data.MissionId,
                Latitude = 9, Longitude = 100, Timestamp = DateTime.UtcNow.AddMinutes(-2) });
            await db.SaveChangesAsync();
        });
        await publisher.PublishAsync();
        Assert.Equal(2, transport.Events.Count);
        await factory.WithDbAsync(async db => {
            var old = await db.FlightMissions.SingleAsync(m => m.Id == data.MissionId);
            old.FlightStatus = "ABORTED";
            db.FlightMissions.Add(new FlightMission { OrderId = data.OrderId, DroneId = old.DroneId,
                FlightStatus = "IN_AIR", CreatedAt = old.CreatedAt.AddMinutes(1) });
            await db.SaveChangesAsync();
        });
        await publisher.PublishAsync();
        Assert.Equal(3, transport.Events.Count);
        Assert.Equal("StatusChanged", transport.Events.Last().Method);
        Assert.Null(connection.LastPosition);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("demoted")]
    [InlineData("expired")]
    [InlineData("reassigned")]
    public async Task Revocation_removes_subscription_without_sending_more_protected_data(string change)
    {
        var fixture = new TrackingApiTests(factory);
        var data = await fixture.SeedAsync();
        var user = change == "demoted" ? await fixture.ActorAsync("ADMIN") : data.Owner;
        var (publisher, subscriptions, transport, clock) = Setup();
        var aborted = false;
        var connection = new TrackingConnection("viewer", user, clock.GetUtcNow().AddMinutes(10), () => aborted = true);
        connection.Select(data.OrderId);
        subscriptions.Add(connection);
        await publisher.PublishAsync();
        var count = transport.Events.Count;
        await factory.WithDbAsync(async db => {
            var actor = await db.Users.FindAsync(user);
            if (change == "inactive") actor!.Status = "INACTIVE";
            if (change == "demoted") actor!.RoleId = (await db.Roles.SingleAsync(r => r.Code == "CUSTOMER")).Id;
            if (change == "reassigned") {
                var other = new User { RoleId = actor!.RoleId, Email = $"new-owner-{Guid.NewGuid():N}@example.com" };
                db.Users.Add(other);
                (await db.DeliveryOrders.FindAsync(data.OrderId))!.Customer = other;
            }
            await db.SaveChangesAsync();
        });
        if (change == "expired") clock.Now = clock.Now.AddMinutes(11);
        await publisher.PublishAsync();
        Assert.True(aborted);
        Assert.Null(subscriptions.Get("viewer"));
        Assert.Equal(count, transport.Events.Count);
    }

    [Fact]
    public async Task Failed_transport_does_not_stop_other_subscribers_or_modify_the_order()
    {
        var fixture = new TrackingApiTests(factory);
        var data = await fixture.SeedAsync();
        var (publisher, subscriptions, transport, clock) = Setup();
        foreach (var id in new[] { "broken", "healthy" }) {
            var connection = new TrackingConnection(id, data.Owner, clock.Now.AddMinutes(10), () => { });
            connection.Select(data.OrderId);
            subscriptions.Add(connection);
        }
        transport.FailConnection = "broken";
        await publisher.PublishAsync();
        Assert.Equal(2, transport.Events.Count(e => e.Connection == "healthy"));
        await factory.WithDbAsync(async db => Assert.Equal("IN_TRANSIT", (await db.DeliveryOrders.FindAsync(data.OrderId))!.Status));
        transport.FailConnection = null;
        await publisher.PublishAsync();
        Assert.Equal(2, transport.Events.Count(e => e.Connection == "broken"));
        Assert.Equal(2, transport.Events.Count(e => e.Connection == "healthy"));
    }

    [Fact]
    public async Task Sixty_seconds_is_fresh_and_one_tick_later_is_stale()
    {
        var fixture = new TrackingApiTests(factory);
        var data = await fixture.SeedAsync();
        await factory.WithDbAsync(async db => {
            var sample = await db.FlightTelemetryLogs.SingleAsync(t => t.MissionId == data.MissionId);
            var clock = new TrackingClock(new DateTimeOffset(DateTime.SpecifyKind(sample.Timestamp, DateTimeKind.Utc)).AddSeconds(60));
            var service = new TrackingService(db, clock);
            Assert.False((await service.GetAsync(data.Owner, data.OrderId)).Snapshot.TelemetryStale);
            clock.Now = clock.Now.AddTicks(1);
            Assert.True((await service.GetAsync(data.Owner, data.OrderId)).Snapshot.TelemetryStale);
        });
    }

    private (TrackingBroadcaster Publisher, TrackingSubscriptions Subscriptions, RecordingTrackingHub Transport, TrackingClock Clock) Setup()
    {
        var subscriptions = new TrackingSubscriptions();
        var transport = new RecordingTrackingHub();
        var clock = new TrackingClock(DateTimeOffset.UtcNow);
        var publisher = new TrackingBroadcaster(factory.Services.GetRequiredService<IServiceScopeFactory>(),
            subscriptions, transport, clock, NullLogger<TrackingBroadcaster>.Instance);
        return (publisher, subscriptions, transport, clock);
    }
}

internal sealed class TrackingClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class RecordingTrackingHub : IHubContext<TrackingHub>
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<(string Connection, string Method, object?[] Args)> _events = new();
    public IReadOnlyList<(string Connection, string Method, object?[] Args)> Events => _events.ToArray();
    public string? FailConnection { get; set; }
    public string? BlockConnection { get; set; }
    public TaskCompletionSource SendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseSend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IHubClients Clients => new RecordingClients(this);
    public IGroupManager Groups => throw new NotSupportedException();

    private sealed class Proxy(RecordingTrackingHub hub, string connection) : IClientProxy
    {
        public async Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            if (hub.FailConnection == connection) throw new IOException("Disconnected transport.");
            if (hub.BlockConnection == connection) {
                hub.SendStarted.TrySetResult();
                await hub.ReleaseSend.Task.WaitAsync(cancellationToken);
            }
            hub._events.Enqueue((connection, method, args));
        }
    }

    private sealed class RecordingClients(RecordingTrackingHub hub) : IHubClients
    {
        public IClientProxy Client(string connectionId) => new Proxy(hub, connectionId);
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy Group(string groupName) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }
}
