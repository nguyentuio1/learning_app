using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

public static class MissionResources
{
    // Caller owns the order lock and transaction. Never clear historical slot links:
    // those slots can already have been reused for a different parcel.
    public static async Task AbortPlannedAsync(AppDbContext db, DeliveryOrder order, DateTime now, CancellationToken ct = default)
    {
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM landing_stations WHERE \"Id\" IN ({order.OriginStationId}, {order.DestinationStationId}) ORDER BY \"Id\" FOR SHARE", ct);
        var mission = db.Database.IsNpgsql()
            ? (await db.FlightMissions.FromSqlInterpolated($"SELECT * FROM flight_missions WHERE \"OrderId\" = {order.Id} AND \"FlightStatus\" = 'PLANNED' FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            : await db.FlightMissions.SingleOrDefaultAsync(m => m.OrderId == order.Id && m.FlightStatus == "PLANNED", ct);
        if (mission is null) return;
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM drones WHERE \"Id\" = {mission.DroneId} FOR UPDATE", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM station_slots WHERE \"Id\" IN ({mission.StartSlotId}, {mission.DestinationSlotId}) ORDER BY \"Id\" FOR UPDATE", ct);
        }
        var slots = await db.StationSlots.Where(s => s.Id == mission.StartSlotId || s.Id == mission.DestinationSlotId).ToListAsync(ct);
        foreach (var slot in slots)
        {
            if (!await IsLatestSlotClaimAsync(db, mission, slot.Id, ct)) continue;
            if (slot.Status == "OCCUPIED")
                throw new ApiException(StatusCodes.Status409Conflict, "The parcel must be physically removed before releasing its planned assignment.");
            if (slot.Status == "RESERVED") { slot.Status = "EMPTY"; slot.LastUpdated = now; }
        }
        var drone = await db.Drones.SingleAsync(d => d.Id == mission.DroneId, ct);
        if (drone.Status == "ASSIGNED" && !await db.FlightMissions.AnyAsync(m => m.Id != mission.Id && m.DroneId == drone.Id
            && (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR"), ct)) drone.Status = "IDLE";
        mission.FlightStatus = "ABORTED";
        mission.EndTime = now;
    }

    public static async Task<bool> IsLatestSlotClaimAsync(AppDbContext db, FlightMission mission, Guid slotId, CancellationToken ct = default)
    {
        var latestId = await db.FlightMissions.AsNoTracking().Where(m => m.StartSlotId == slotId || m.DestinationSlotId == slotId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);
        return latestId == mission.Id;
    }
}
