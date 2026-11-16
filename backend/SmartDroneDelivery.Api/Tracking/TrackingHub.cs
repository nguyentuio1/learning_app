using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Tracking;

[Authorize]
public sealed class TrackingHub(ITrackingService tracking, TrackingSubscriptions subscriptions, TimeProvider clock) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
            || !long.TryParse(Context.User?.FindFirst(JwtRegisteredClaimNames.Exp)?.Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var expiry))
        {
            Context.Abort();
            throw new HubException("Tracking unavailable.");
        }
        try { await tracking.AuthorizeViewerAsync(userId, Context.ConnectionAborted); }
        catch (ApiException) { Context.Abort(); throw new HubException("Tracking unavailable."); }
        subscriptions.Add(new(Context.ConnectionId, userId, DateTimeOffset.FromUnixTimeSeconds(expiry), Context.Abort));
        await base.OnConnectedAsync();
    }

    public async Task Subscribe(string orderId)
    {
        if (!Guid.TryParse(orderId, out var id)) throw new HubException("Tracking unavailable.");
        var connection = subscriptions.Get(Context.ConnectionId)
            ?? throw new HubException("Tracking unavailable.");
        await connection.Gate.WaitAsync(Context.ConnectionAborted);
        try
        {
            if (connection.ExpiresAt <= clock.GetUtcNow())
            {
                subscriptions.Remove(Context.ConnectionId);
                Context.Abort();
                throw new HubException("Tracking unavailable.");
            }
            try { await tracking.GetAsync(connection.UserId, id, Context.ConnectionAborted); }
            catch (ApiException) { throw new HubException("Tracking unavailable."); }
            connection.Select(id);
        }
        finally { connection.Gate.Release(); }
    }

    public async Task Unsubscribe()
    {
        var connection = subscriptions.Get(Context.ConnectionId);
        if (connection is null) return;
        await connection.Gate.WaitAsync(Context.ConnectionAborted);
        try { connection.Select(null); }
        finally { connection.Gate.Release(); }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        subscriptions.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
