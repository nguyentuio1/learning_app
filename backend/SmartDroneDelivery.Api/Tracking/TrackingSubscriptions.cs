using System.Collections.Concurrent;
using SmartDroneDelivery.Api.DTOs;

namespace SmartDroneDelivery.Api.Tracking;

public sealed class TrackingConnection(string connectionId, Guid userId, DateTimeOffset expiresAt, Action abort)
{
    public string ConnectionId { get; } = connectionId;
    public Guid UserId { get; } = userId;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public Action Abort { get; } = abort;
    // Subscription changes and sends are serialized, so no old-order send follows a new Subscribe acknowledgment.
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public Guid? OrderId { get; set; }
    public string? LastStatus { get; set; }
    public Guid? LastMissionId { get; set; }
    public TelemetryPosition? LastPosition { get; set; }
    public bool HasPublished { get; set; }

    public void Select(Guid? orderId)
    {
        OrderId = orderId;
        LastStatus = null;
        LastMissionId = null;
        LastPosition = null;
        HasPublished = false;
    }
}

public sealed class TrackingSubscriptions
{
    private readonly ConcurrentDictionary<string, TrackingConnection> _connections = new();
    public TrackingConnection[] Connections => _connections.Values.ToArray();
    public void Add(TrackingConnection connection) => _connections[connection.ConnectionId] = connection;
    public TrackingConnection? Get(string id) => _connections.GetValueOrDefault(id);
    public void Remove(string id) => _connections.TryRemove(id, out _);
}
