using Microsoft.AspNetCore.SignalR;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Tracking;

/// <summary>Reads committed facts rather than publishing from inside a workflow transaction.</summary>
public sealed class TrackingBroadcaster(IServiceScopeFactory scopes, TrackingSubscriptions subscriptions,
    IHubContext<TrackingHub> hub, TimeProvider clock, ILogger<TrackingBroadcaster> logger)
{
    public Task PublishAsync(CancellationToken ct = default) => Parallel.ForEachAsync(subscriptions.Connections,
        new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, async (connection, token) =>
        {
            // Do not let a slow Subscribe or another pass block other viewers.
            if (!await connection.Gate.WaitAsync(TimeSpan.Zero, token)) return;
            try
            {
                if (!ReferenceEquals(subscriptions.Get(connection.ConnectionId), connection)) return;
                if (connection.ExpiresAt <= clock.GetUtcNow()) { Revoke(connection); return; }
                if (connection.OrderId is not { } orderId) return;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                using var scope = scopes.CreateScope();
                var tracking = scope.ServiceProvider.GetRequiredService<ITrackingService>();
                var read = await tracking.GetAsync(connection.UserId, orderId, timeout.Token);
                var snapshot = read.Snapshot;
                var client = hub.Clients.Client(connection.ConnectionId);
                var missionChanged = connection.LastMissionId != read.MissionId;
                if (!connection.HasPublished || connection.LastStatus != snapshot.Status || missionChanged)
                {
                    await client.SendAsync("StatusChanged", new OrderDecisionDto(snapshot.OrderId,
                        snapshot.TrackingNumber, snapshot.Status, read.UpdatedAt), timeout.Token);
                    connection.LastStatus = snapshot.Status;
                }
                if (snapshot.LastPosition is { } position &&
                    (missionChanged || connection.LastPosition is null ||
                     position.Timestamp > connection.LastPosition.Timestamp ||
                     (position.Timestamp == connection.LastPosition.Timestamp && position != connection.LastPosition)))
                {
                    await client.SendAsync("PositionUpdated", position, timeout.Token);
                    connection.LastPosition = position;
                }
                if (snapshot.LastPosition is null) connection.LastPosition = null;
                connection.LastMissionId = read.MissionId;
                connection.HasPublished = true;
            }
            catch (ApiException error) when (error.StatusCode is 401 or 403 or 404) { Revoke(connection); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                // A failed read/send cannot roll back persisted workflow state. Retry next tick.
                logger.LogWarning(error, "Tracking refresh failed for connection {ConnectionId}.", connection.ConnectionId);
            }
            finally { connection.Gate.Release(); }
        });

    private void Revoke(TrackingConnection connection)
    {
        subscriptions.Remove(connection.ConnectionId);
        connection.Select(null);
        connection.Abort();
    }
}
