using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public interface ITrackingService
{
    Task<string> AuthorizeViewerAsync(Guid userId, CancellationToken ct = default);
    Task<TrackingRead> GetAsync(Guid userId, Guid orderId, CancellationToken ct = default);
}

public class TrackingService(AppDbContext db, TimeProvider clock) : ITrackingService
{
    public async Task<string> AuthorizeViewerAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.Status, Role = u.Role.Code }).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(401, "Missing, invalid or expired token.");
        if (user.Status != "ACTIVE" || user.Role is not
            (AppRoles.Customer or AppRoles.Dispatcher or AppRoles.Manager or AppRoles.Admin))
            throw new ApiException(403, "You do not have permission to track deliveries.");
        return user.Role;
    }

    public async Task<TrackingRead> GetAsync(Guid userId, Guid orderId, CancellationToken ct = default)
    {
        var role = await AuthorizeViewerAsync(userId, ct);
        var row = await db.DeliveryOrders.AsNoTracking()
            .Where(o => o.Id == orderId && (role != AppRoles.Customer || o.CustomerId == userId))
            .Select(o => new {
                o.Id, o.TrackingNumber, o.Status, o.EstimatedDeliveryTime, o.UpdatedAt,
                Origin = new TrackingStation(o.OriginStation.Id, o.OriginStation.Name,
                    o.OriginStation.Latitude, o.OriginStation.Longitude),
                Destination = new TrackingStation(o.DestinationStation.Id, o.DestinationStation.Name,
                    o.DestinationStation.Latitude, o.DestinationStation.Longitude),
                Mission = o.FlightMissions.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
                    .Select(m => new {
                        m.Id, m.FlightStatus,
                        Sample = m.TelemetryLogs.OrderByDescending(t => t.Timestamp).ThenByDescending(t => t.Id)
                            .Select(t => new TelemetryPosition(t.Latitude, t.Longitude, t.AltitudeMeters,
                                t.SpeedMps, t.BatteryPercentage, t.Timestamp)).FirstOrDefault()
                    }).FirstOrDefault()
            }).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(404, "Delivery order not found.");

        var inAir = row.Status == "IN_TRANSIT" && row.Mission?.FlightStatus == "IN_AIR";
        var position = inAir ? row.Mission?.Sample : null;
        if (position is not null)
            position = position with { Timestamp = DateTime.SpecifyKind(position.Timestamp, DateTimeKind.Utc) };
        var stale = inAir && (position is null ||
            clock.GetUtcNow().UtcDateTime - position.Timestamp > TimeSpan.FromSeconds(60));
        return new(new(row.Id, row.TrackingNumber, row.Status, row.Origin, row.Destination,
            row.EstimatedDeliveryTime is { } eta ? DateTime.SpecifyKind(eta, DateTimeKind.Utc) : null,
            position, stale), row.Mission?.Id, DateTime.SpecifyKind(row.UpdatedAt, DateTimeKind.Utc));
    }
}
