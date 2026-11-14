using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

public partial class StationOperationService
{
    public async Task<ClearExpiredParcelResponseDto> ClearExpiredParcelAsync(Guid userId, Guid stationId,
        ClearExpiredParcelRequest request, CancellationToken cancellationToken = default)
    {
        using var operation = await _transitions.EnterOperationAsync(cancellationToken);
        await AuthorizeStationOperatorAsync(userId, stationId);
        var errors = new Dictionary<string, List<string>>();
        if (request.SlotId is null || request.SlotId == Guid.Empty)
            AddError(errors, "slotId", "A non-empty slot ID is required.");
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 5 or > 500)
            AddError(errors, "reason", "Reason is required and must be between 5 and 500 characters.");
        ThrowIfInvalid(errors);
        var slotId = request.SlotId!.Value;

        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
        var slotStation = await _db.StationSlots.AsNoTracking().Where(s => s.Id == slotId)
            .Select(s => (Guid?)s.StationId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(404, "Station slot not found.");
        if (slotStation != stationId) throw ExpiredParcelConflict();

        // Discovery does not lock the slot first: every order workflow takes order -> station -> slot.
        // This is only a candidate. Recheck both ownership and occupancy after taking those locks.
        var candidate = await _db.FlightMissions.AsNoTracking()
            .Where(m => m.StartSlotId == slotId || m.DestinationSlotId == slotId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Select(m => new { m.Id, m.OrderId, m.DestinationSlotId }).FirstOrDefaultAsync(cancellationToken);
        if (candidate is null || candidate.DestinationSlotId != slotId) throw ExpiredParcelConflict();

        var order = _db.Database.IsNpgsql()
            ? (await _db.DeliveryOrders.FromSqlInterpolated($"SELECT * FROM delivery_orders WHERE \"Id\" = {candidate.OrderId} FOR UPDATE")
                .ToListAsync(cancellationToken)).SingleOrDefault()
            : await _db.DeliveryOrders.SingleOrDefaultAsync(o => o.Id == candidate.OrderId, cancellationToken);
        if (order is null || order.Status != "FAILED" || order.DestinationStationId != stationId)
            throw ExpiredParcelConflict();
        if (_db.Database.IsNpgsql())
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM landing_stations WHERE \"Id\" = {stationId} FOR SHARE", cancellationToken);
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM station_slots WHERE \"Id\" = {slotId} FOR UPDATE", cancellationToken);
        }
        // Recheck assignment as well as role after locking the station, so reassignment cannot
        // race a physical-removal confirmation from an operator of the previous assignment.
        await AuthorizeStationOperatorAsync(userId, stationId);
        var slot = await _db.StationSlots.SingleOrDefaultAsync(s => s.Id == slotId, cancellationToken);
        if (slot is null || slot.StationId != stationId || slot.Status != "OCCUPIED") throw ExpiredParcelConflict();
        var latestClaim = await _db.FlightMissions.AsNoTracking()
            .Where(m => m.StartSlotId == slotId || m.DestinationSlotId == slotId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Select(m => m.Id).FirstOrDefaultAsync(cancellationToken);
        var readySource = await _db.FlightMissions.AsNoTracking().AnyAsync(m => m.StartSlotId == slotId
            && m.FlightStatus == "PLANNED" && m.Order.Status == "READY_FOR_TAKEOFF", cancellationToken);
        if (latestClaim != candidate.Id || readySource) throw ExpiredParcelConflict();

        var failedLog = await _db.DeliveryStatusLogs.Include(l => l.FailureEpisode)
            .Where(l => l.OrderId == order.Id && l.ToStatus == "FAILED")
            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id).FirstOrDefaultAsync(cancellationToken);
        if (failedLog is null || failedLog.FromStatus != "ARRIVED"
            || (failedLog.FailureEpisode is { } episode
                ? episode.FailureType != "COLLECTION_TIMEOUT" || episode.Source != "PICKUP_TIMEOUT"
                : failedLog.Remarks != PickupTimeoutService.LogRemarks))
            throw ExpiredParcelConflict();

        var now = DateTime.UtcNow;
        slot.Status = "EMPTY";
        slot.LastUpdated = now;
        order.UpdatedAt = now;
        // An operational note is not a new transition into FAILED. Keep the original
        // episode, its immutable reason, its timestamp and all mission history intact.
        failedLog.Remarks = $"{failedLog.Remarks}\nExpired parcel cleared from slot {slotId}: {reason}";
        _db.AuditLogs.Add(new AuditLog {
            UserId = userId, Action = "EXPIRED_PARCEL_CLEARED", EntityName = "StationSlot",
            EntityId = slotId.ToString(), Reason = reason, Timestamp = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);
        return new(order.Id, order.TrackingNumber, slotId, slot.Status, now);
    }

    private static ApiException ExpiredParcelConflict() => new(409,
        "The slot must currently hold an expired parcel belonging to a failed order at this destination station.");
}
