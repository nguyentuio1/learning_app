using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Api.Services;

/// <summary>Trusted software flight events. Each mission tick is one atomic operation.</summary>
public partial class DroneSimulatorService(AppDbContext db, IOrderTransitionWriter transitions,
    IFlightTelemetryService telemetry, TimeProvider clock, IOptions<DroneSimulatorOptions> options,
    IOptions<DeliveryFlightOptions> flight)
{
    public async Task TickAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!options.Value.Enabled) return;
        // Npgsql persists timestamps at microsecond precision. Normalize before comparing
        // and inserting, so a fixed clock containing sub-microsecond ticks stays idempotent.
        var utc = clock.GetUtcNow().UtcDateTime;
        var now = new DateTime(utc.Ticks - utc.Ticks % 10, DateTimeKind.Utc);
        var candidates = await db.FlightMissions.AsNoTracking()
            .Where(m => m.FlightStatus == "IN_AIR" || db.SimulatorDiversions.Any(d => d.MissionId == m.Id && d.LandedAt == null) || (m.FlightStatus == "PLANNED"
                && m.Order.Status == "READY_FOR_TAKEOFF" && m.Order.ScheduledDepartureTime <= now))
            .OrderBy(m => m.OrderId).ThenBy(m => m.Id).Select(m => new { m.Id, m.OrderId }).ToListAsync(ct);
        var existingEntities = db.ChangeTracker.Entries().Select(e => e.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var candidate in candidates)
        {
            try { await TickMissionAsync(candidate.Id, candidate.OrderId, now, ct); }
            finally
            {
                // A trusted caller can own this context/transaction. Keep their
                // existing tracked entities; release only entities loaded by this tick.
                foreach (var entry in db.ChangeTracker.Entries().Where(e => !existingEntities.Contains(e.Entity)).ToList())
                    entry.State = EntityState.Detached;
            }
        }
    }

    private async Task TickMissionAsync(Guid missionId, Guid orderId, DateTime now, CancellationToken ct)
    {
        using var operation = await transitions.EnterOperationAsync(ct);
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct) : null;
        // Same global order as intake/cancel/assignment: order, stations, mission,
        // drone, slots. Never lock a mission then request its order lock.
        var order = db.Database.IsNpgsql()
            ? (await db.DeliveryOrders.FromSqlInterpolated($"SELECT * FROM delivery_orders WHERE \"Id\" = {orderId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            : await db.DeliveryOrders.SingleOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return;
        // Tracking queries may return an entity previously loaded by the caller.
        // Reload after its row lock so eligibility uses committed/current facts.
        await db.Entry(order).ReloadAsync(ct);
        var recovering = await db.SimulatorDiversions.AsNoTracking().AnyAsync(d => d.MissionId == missionId && d.LandedAt == null, ct);
        if (!recovering && order.Status is not ("READY_FOR_TAKEOFF" or "IN_TRANSIT")) return;
        // A diversion may use any designated operational station. Lock the complete
        // candidate set in the same sorted order before locking mission/drone rows.
        var allStations = recovering || options.Value.AbortAfterSeconds.HasValue;
        var stations = allStations
            ? db.Database.IsNpgsql()
                ? await db.LandingStations.FromSqlRaw("SELECT * FROM landing_stations ORDER BY \"Id\" FOR SHARE").ToListAsync(ct)
                : await db.LandingStations.ToListAsync(ct)
            : db.Database.IsNpgsql()
            ? await db.LandingStations.FromSqlInterpolated($"SELECT * FROM landing_stations WHERE \"Id\" IN ({order.OriginStationId}, {order.DestinationStationId}) ORDER BY \"Id\" FOR SHARE").ToListAsync(ct)
            : await db.LandingStations.Where(s => s.Id == order.OriginStationId || s.Id == order.DestinationStationId).ToListAsync(ct);
        foreach (var station in stations) await db.Entry(station).ReloadAsync(ct);
        var origin = stations.SingleOrDefault(s => s.Id == order.OriginStationId);
        var destination = stations.SingleOrDefault(s => s.Id == order.DestinationStationId);
        var mission = db.Database.IsNpgsql()
            ? (await db.FlightMissions.FromSqlInterpolated($"SELECT * FROM flight_missions WHERE \"Id\" = {missionId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            : await db.FlightMissions.SingleOrDefaultAsync(m => m.Id == missionId, ct);
        if (mission is null) return;
        await db.Entry(mission).ReloadAsync(ct);
        if (recovering)
        {
            await RecoverAsync(mission, stations, now, ct);
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return;
        }
        if (origin is null || destination is null) return;
        if (mission.OrderId != order.Id || mission.FlightStatus is not ("PLANNED" or "IN_AIR")
            || mission.StartSlotId is null || mission.DestinationSlotId is null || mission.StartSlotId == mission.DestinationSlotId) return;
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM drones WHERE \"Id\" = {mission.DroneId} FOR UPDATE", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM station_slots WHERE \"Id\" IN ({mission.StartSlotId}, {mission.DestinationSlotId}) ORDER BY \"Id\" FOR UPDATE", ct);
        }
        var drone = await db.Drones.SingleOrDefaultAsync(d => d.Id == mission.DroneId, ct);
        var slots = await db.StationSlots.Where(s => s.Id == mission.StartSlotId || s.Id == mission.DestinationSlotId).ToListAsync(ct);
        if (drone is not null) await db.Entry(drone).ReloadAsync(ct);
        foreach (var slot in slots) await db.Entry(slot).ReloadAsync(ct);
        var source = slots.SingleOrDefault(s => s.Id == mission.StartSlotId);
        var target = slots.SingleOrDefault(s => s.Id == mission.DestinationSlotId);
        var distance = DeliveryRouteEstimate.DistanceKm(origin.Latitude, origin.Longitude, destination.Latitude, destination.Longitude) * 1000;
        if (mission.FlightStatus == "IN_AIR" && order.Status == "IN_TRANSIT" && mission.StartTime is { } started
            && mission.EndTime is null && drone is { Status: "FLYING", CurrentStationId: null } && now > started
            && options.Value.AbortAfterSeconds is { } abortAfter && (now - started).TotalSeconds >= abortAfter
            && (now - started).TotalSeconds * flight.Value.CruiseSpeedMps < distance)
        {
            var latest = await db.FlightTelemetryLogs.AsNoTracking().Where(l => l.MissionId == mission.Id).MaxAsync(l => (DateTime?)l.Timestamp, ct);
            if (latest > now) return;
            var fractionAtAbort = Math.Clamp((now - started).TotalSeconds * flight.Value.CruiseSpeedMps / distance, 0, 1);
            var atAbort = Position(origin, destination, fractionAtAbort, distance);
            await AbortAsync(order, mission, stations, atAbort, now, ct);
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return;
        }
        if (origin.Status != "OPERATIONAL" || destination.Status != "OPERATIONAL") return;
        var package = await db.Packages.AsNoTracking().SingleOrDefaultAsync(p => p.Id == order.PackageId, ct);
        if (drone is null || package is null || package.WeightGram <= 0 || package.WeightGram > drone.MaxPayloadGram
            || distance / 1000 * 1.3 > (double)drone.MaxFlightRangeKm
            || source is null || source.StationId != origin.Id || target is null || target.StationId != destination.Id || target.Status != "RESERVED"
            || !await MissionResources.IsLatestSlotClaimAsync(db, mission, target.Id, ct)
            || await HasOtherReadySourceAsync(mission.Id, target.Id, ct)
            || await db.FlightMissions.AnyAsync(m => m.Id != mission.Id && m.DroneId == drone.Id && (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR"), ct)) return;

        if (mission.FlightStatus == "PLANNED")
        {
            if (order.Status != "READY_FOR_TAKEOFF" || order.ScheduledDepartureTime is null || order.ScheduledDepartureTime > now
                || mission.ScheduledDepartureTime is null || mission.ScheduledDepartureTime > now
                || order.EstimatedDeliveryTime is null || mission.StartTime is not null || mission.EndTime is not null
                || drone.Status != "ASSIGNED" || drone.CurrentStationId != origin.Id
                || source.Status != "OCCUPIED" || await HasOtherReadySourceAsync(mission.Id, source.Id, ct)) return;
            // Current READY intake linkage owns the occupied source; historical mission
            // creation order is not evidence of when a source slot was physically claimed.
            await transitions.StageAsync(order, "READY_FOR_TAKEOFF", "IN_TRANSIT", null, "Simulated takeoff", now, ct);
            order.ActualDepartureTime = mission.StartTime = now;
            mission.FlightStatus = "IN_AIR";
            drone.Status = "FLYING"; drone.CurrentStationId = null;
            source.Status = "EMPTY"; source.LastUpdated = now;
            Notify(order, "IN_TRANSIT", now);
        }
        if (order.Status != "IN_TRANSIT" || mission.StartTime is null || mission.EndTime is not null
            || drone.Status != "FLYING" || drone.CurrentStationId is not null || now < mission.StartTime.Value) return;
        var latestAt = await db.FlightTelemetryLogs.AsNoTracking().Where(l => l.MissionId == mission.Id).MaxAsync(l => (DateTime?)l.Timestamp, ct);
        if (latestAt >= now) return;
        var elapsed = (now - mission.StartTime.Value).TotalSeconds;
        // Even a colocated route gets a departure event/sample first. Its next
        // strictly later tick reports landing; one tick stages at most one transition.
        var fraction = distance <= 0 ? (elapsed > 0 ? 1 : 0) : Math.Clamp(elapsed * flight.Value.CruiseSpeedMps / distance, 0, 1);
        var arrived = fraction >= 1;
        var position = Position(origin, destination, fraction, distance);
        if (!IsTelemetryLost(elapsed)) telemetry.Stage(mission, new TelemetrySampleRequest {
            Latitude = position.Latitude, Longitude = position.Longitude,
            AltitudeMeters = arrived ? 0 : options.Value.AltitudeMeters,
            SpeedMps = arrived ? 0 : (decimal)flight.Value.CruiseSpeedMps,
            BatteryPercentage = (int)Math.Round(100 - 20 * fraction), Timestamp = now.ToString("O")
        });
        if (arrived)
        {
            await transitions.StageAsync(order, "IN_TRANSIT", "ARRIVED", null, "Simulated arrival at destination", now, ct);
            mission.FlightStatus = "COMPLETED"; mission.EndTime = now;
            target.Status = "OCCUPIED"; target.LastUpdated = now;
            drone.Status = "CHARGING"; drone.CurrentStationId = destination.Id;
            Notify(order, "ARRIVED", now);
        }
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
    }

    private Task<bool> HasOtherReadySourceAsync(Guid missionId, Guid slotId, CancellationToken ct) =>
        db.FlightMissions.AnyAsync(m => m.Id != missionId && m.StartSlotId == slotId && m.FlightStatus == "PLANNED"
            && m.Order.Status == "READY_FOR_TAKEOFF", ct);

    private void Notify(DeliveryOrder order, string status, DateTime now) => db.Notifications.Add(new Notification {
        UserId = order.CustomerId, OrderId = order.Id, Type = "ORDER_STATUS_CHANGED", Title = "Order status changed",
        Message = $"Your delivery order {order.TrackingNumber} is now {status}.", CreatedAt = now
    });

    private static (decimal Latitude, decimal Longitude) Position(LandingStation origin, LandingStation destination, double fraction, double distance)
    {
        if (fraction >= 1) return (destination.Latitude, destination.Longitude);
        if (fraction <= 0 || distance < 0.000001) return (origin.Latitude, origin.Longitude);
        const double radians = Math.PI / 180;
        var lat1 = (double)origin.Latitude * radians; var lat2 = (double)destination.Latitude * radians;
        var lon1 = (double)origin.Longitude * radians;
        var delta = (double)(destination.Longitude - origin.Longitude) * radians;
        var bearing = Math.Atan2(Math.Sin(delta) * Math.Cos(lat2), Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(delta));
        var angle = distance / 6371008.8 * fraction;
        var lat = Math.Asin(Math.Clamp(Math.Sin(lat1) * Math.Cos(angle) + Math.Cos(lat1) * Math.Sin(angle) * Math.Cos(bearing), -1, 1));
        var lon = lon1 + Math.Atan2(Math.Sin(bearing) * Math.Sin(angle) * Math.Cos(lat1), Math.Cos(angle) - Math.Sin(lat1) * Math.Sin(lat));
        var degrees = (lon / radians + 540) % 360 - 180;
        return (Math.Clamp((decimal)(lat / radians), -90, 90), Math.Clamp((decimal)degrees, -180, 180));
    }
}

public class DroneSimulatorWorker(IServiceScopeFactory scopes, IOptions<DroneSimulatorOptions> options,
    TimeProvider clock, ILogger<DroneSimulatorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.TickIntervalSeconds), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<DroneSimulatorService>().TickAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception)
                {
                    // Do not log provider exception text, tracked entities, or pickup codes.
                    logger.LogError("Drone simulator tick failed; retrying at the next interval.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
