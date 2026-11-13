using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public class FailureService(AppDbContext db, IOrderTransitionWriter transitions, TimeProvider time)
{
    public async Task<OrderDecisionDto> ReportAsync(Guid id, Guid actorId, ReportFailureRequest request, CancellationToken ct = default)
    {
        await EnsureStaffAsync(actorId, false, ct);
        ValidateFlightType(request.FailureType);
        var reason = RequiredText(request.Reason, "reason");
        using var operation = await transitions.EnterOperationAsync(ct);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var order = await LockOrderAsync(id, ct);
        await StageInAirFailureAsync(order, request.FailureType!, reason, actorId, "DISPATCH_REPORT", time.GetUtcNow().UtcDateTime, ct);
        await SaveAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Decision(order);
    }

    /// <summary>
    /// Trusted system/dispatcher staging entry point. Caller already owns the operation gate, order lock and
    /// relational transaction. Does not save, commit, enter a gate, or claim a physical landing.
    /// </summary>
    public async Task StageInAirFailureAsync(DeliveryOrder order, string type, string reason, Guid? actorId,
        string source, DateTime now, CancellationToken ct = default)
    {
        ValidateFlightType(type);
        reason = RequiredText(reason, "reason");
        if (db.Database.IsRelational() && db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Failure staging requires the caller's transaction.");
        if (order.Status != "IN_TRANSIT") throw Conflict("Only an in-transit flight can be reported failed.");
        var mission = db.Database.IsNpgsql()
            ? (await db.FlightMissions.FromSqlInterpolated($"SELECT * FROM flight_missions WHERE \"OrderId\" = {order.Id} AND \"FlightStatus\" = 'IN_AIR' FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            : await db.FlightMissions.SingleOrDefaultAsync(m => m.OrderId == order.Id && m.FlightStatus == "IN_AIR", ct);
        if (mission is null) throw Conflict("The order has no in-air mission.");
        var log = await transitions.StageAsync(order, "IN_TRANSIT", "FAILED", actorId, reason, now, ct);
        FailureRecording.Record(db, log, type, source);
        order.CancelReason = null;
        mission.FlightStatus = "ABORTED"; mission.EndTime = now;
        Notify(order.CustomerId, order, "Delivery failed", reason, now);
        Audit(order, actorId, "DELIVERY_FAILURE_DETECTED", now);
    }

    public async Task<OrderDecisionDto> ResolveAsync(Guid id, Guid actorId, ResolveFailureRequest request, CancellationToken ct = default)
    {
        await EnsureStaffAsync(actorId, false, ct);
        if (request.Action is not ("RESCHEDULE" or "RETURN_TO_ORIGIN" or "CANCEL")) throw Field("action", "Unsupported resolution action.");
        var note = RequiredText(request.Note, "note");
        if (request.Action == "RESCHEDULE" && request.PackageVerified != true) throw Field("packageVerified", "Verify the package before rescheduling.");
        using var operation = await transitions.EnterOperationAsync(ct);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var order = await LockOrderAsync(id, ct);
        if (order.Status != "FAILED") throw Conflict("The failure is no longer available for a decision.");
        var log = await db.DeliveryStatusLogs.Include(l => l.FailureEpisode).Where(l => l.OrderId == id && l.ToStatus == "FAILED")
            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id).FirstOrDefaultAsync(ct)
            ?? throw Conflict("The order has no recorded failure episode.");
        // Only legacy episodes use the old control marker: intake also stores human reason text here.
        if (log.FailureEpisode is null && order.CancelReason == "RETURN_REQUESTED")
            throw Conflict("A return was already requested for this legacy episode.");
        if (await db.FailureHandlingEvents.AnyAsync(e => e.FailureLogId == log.Id && (e.Action == "RETURN_TO_ORIGIN" || e.Action == "RESCHEDULE" || e.Action == "CANCEL"), ct))
            throw Conflict("A handling decision was already recorded for this episode.");
        var now = time.GetUtcNow().UtcDateTime;
        if (request.Action != "RETURN_TO_ORIGIN") await ReleaseSafeResourcesAsync(order, now, ct);
        db.FailureHandlingEvents.Add(new() { FailureLog = log, Action = request.Action, Note = note, PerformedBy = actorId, CreatedAt = now });
        if (request.Action == "RETURN_TO_ORIGIN")
        {
            order.CancelReason = "RETURN_REQUESTED"; order.UpdatedAt = now;
            var operatorId = await db.LandingStations.Where(s => s.Id == order.OriginStationId).Select(s => s.OperatorId).SingleAsync(ct);
            if (operatorId is { } recipient) Notify(recipient, order, "Return requested", $"Return requested for {order.TrackingNumber}: {note}", now);
        }
        else
        {
            await transitions.StageAsync(order, "FAILED", request.Action == "RESCHEDULE" ? "APPROVED" : "CANCELLED", actorId, note, now, ct);
            order.CancelReason = request.Action == "CANCEL" ? note : null;
            if (request.Action == "RESCHEDULE") { order.ScheduledDepartureTime = null; order.EstimatedDeliveryTime = null; }
        }
        Notify(order.CustomerId, order, "Delivery failure handling", note, now);
        Audit(order, actorId, "FAILED_DELIVERY_RESOLVED", now);
        await SaveAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Decision(order);
    }

    private async Task ReleaseSafeResourcesAsync(DeliveryOrder order, DateTime now, CancellationToken ct)
    {
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM landing_stations WHERE \"Id\" IN ({order.OriginStationId}, {order.DestinationStationId}) ORDER BY \"Id\" FOR SHARE", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM flight_missions WHERE \"OrderId\" = {order.Id} ORDER BY \"Id\" FOR UPDATE", ct);
        }
        var missions = await db.FlightMissions.Where(m => m.OrderId == order.Id).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync(ct);
        var droneIds = missions.Select(m => m.DroneId).Distinct().Order().ToArray();
        // All supported failure producers run before intake or after takeoff. A historical source
        // link cannot identify a parcel still held by this failed order after it has flown.
        var slotIds = missions.Select(m => m.DestinationSlotId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().Order().ToArray();
        if (db.Database.IsNpgsql())
        {
            foreach (var droneId in droneIds) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM drones WHERE \"Id\" = {droneId} FOR UPDATE", ct);
            foreach (var slotId in slotIds) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM station_slots WHERE \"Id\" = {slotId} FOR UPDATE", ct);
        }
        var ownedSlots = new List<StationSlot>();
        foreach (var mission in missions)
        foreach (var slotId in new[] { mission.DestinationSlotId }.Where(s => s.HasValue))
        {
            if (!await OwnsDestinationSlotAsync(mission, slotId!.Value, ct)) continue;
            var slot = await db.StationSlots.FindAsync([slotId.Value], ct);
            if (slot is null) continue;
            if (slot.Status == "OCCUPIED") throw Conflict("The parcel must be physically removed before rescheduling or cancelling.");
            ownedSlots.Add(slot);
        }
        foreach (var slot in ownedSlots.Where(s => s.Status == "RESERVED")) { slot.Status = "EMPTY"; slot.LastUpdated = now; }
        foreach (var mission in missions)
        {
            if (mission.FlightStatus == "IN_AIR") throw Conflict("An in-air mission must be failed before resolving the order.");
            if (mission.FlightStatus == "PLANNED") { mission.FlightStatus = "ABORTED"; mission.EndTime = now; }
        }
        foreach (var droneId in droneIds)
        {
            var drone = (await db.Drones.FindAsync([droneId], ct))!;
            // ASSIGNED plus a persisted station is grounded evidence. FLYING and CHARGING remain facts.
            if (drone.Status == "ASSIGNED" && drone.CurrentStationId.HasValue && !await db.FlightMissions.AnyAsync(m => m.DroneId == droneId && m.OrderId != order.Id && (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR"), ct)) drone.Status = "IDLE";
        }
    }

    public async Task<PagedResult<FailedOrderDto>> ListAsync(Guid actorId, FailedOrderFilterQuery filter, CancellationToken ct = default)
    {
        await EnsureStaffAsync(actorId, false, ct);
        var from = SchedulingService.ParseUtc(filter.FromDate, "fromDate"); var to = SchedulingService.ParseUtc(filter.ToDate, "toDate");
        if (from > to) throw Field("toDate", "toDate must be on or after fromDate.");
        var search = filter.SearchKeyword?.Trim();
        if (search?.Length > 50) throw Field("searchKeyword", "Use at most 50 characters.");
        if (filter.FailureType is not (null or "MISSION_ABORTED" or "EMERGENCY_LANDING" or "HARDWARE_FAULT" or "COLLECTION_TIMEOUT" or "UNKNOWN")) throw Field("failureType", "Unsupported failure type.");
        var resolution = filter.ResolutionStatus ?? "UNRESOLVED";
        if (resolution is not ("UNRESOLVED" or "RETURN_REQUESTED" or "RESOLVED" or "ALL")) throw Field("resolutionStatus", "Unsupported resolution status.");
        var logs = db.DeliveryStatusLogs.AsNoTrackingWithIdentityResolution().Where(l => l.ToStatus == "FAILED" && !db.DeliveryStatusLogs.Any(n => n.OrderId == l.OrderId && n.ToStatus == "FAILED" && (n.CreatedAt > l.CreatedAt || (n.CreatedAt == l.CreatedAt && n.Id > l.Id))));
        if (filter.OriginStationId.HasValue) logs = logs.Where(l => l.Order.OriginStationId == filter.OriginStationId);
        if (filter.DestinationStationId.HasValue) logs = logs.Where(l => l.Order.DestinationStationId == filter.DestinationStationId);
        if (!string.IsNullOrEmpty(search)) { var keyword = search.ToUpperInvariant(); logs = logs.Where(l => l.Order.TrackingNumber.ToUpper().Contains(keyword)); }
        if (from.HasValue) logs = logs.Where(l => l.CreatedAt >= from.Value);
        if (to.HasValue) logs = logs.Where(l => l.CreatedAt <= to.Value);
        if (filter.FailureType is { } type) logs = logs.Where(l => (l.FailureEpisode == null ? "UNKNOWN" : l.FailureEpisode.FailureType) == type);
        if (resolution == "UNRESOLVED") logs = logs.Where(l => l.Order.Status == "FAILED"
            && !l.HandlingEvents.Any(e => e.Action == "RETURN_TO_ORIGIN")
            && (l.FailureEpisode != null || l.Order.CancelReason != "RETURN_REQUESTED"));
        if (resolution == "RETURN_REQUESTED") logs = logs.Where(l => l.Order.Status == "FAILED"
            && (l.HandlingEvents.Any(e => e.Action == "RETURN_TO_ORIGIN") || l.FailureEpisode == null && l.Order.CancelReason == "RETURN_REQUESTED"));
        if (resolution == "RESOLVED") logs = logs.Where(l => l.Order.StatusLogs.Any(n => n.FromStatus == "FAILED" && (n.ToStatus == "APPROVED" || n.ToStatus == "CANCELLED") && (n.CreatedAt > l.CreatedAt || (n.CreatedAt == l.CreatedAt && n.Id > l.Id))));
        var count = await logs.CountAsync(ct); var offset = ((long)filter.Page - 1) * filter.PageSize;
        var rows = offset >= count ? [] : await logs.OrderByDescending(l => l.CreatedAt).ThenBy(l => l.OrderId).Skip((int)offset).Take(filter.PageSize)
            .Include(l => l.FailureEpisode).Include(l => l.HandlingEvents).Include(l => l.Order).ThenInclude(o => o.StatusLogs)
            .Include(l => l.Order).ThenInclude(o => o.Package).Include(l => l.Order).ThenInclude(o => o.OriginStation).Include(l => l.Order).ThenInclude(o => o.DestinationStation).AsSplitQuery().ToListAsync(ct);
        var items = rows.Select(l => { var e = Episode(l, l.Order); var o = l.Order; return new FailedOrderDto(o.Id, o.TrackingNumber, o.Status, o.Package.PackageName,
            new(o.OriginStationId, o.OriginStation.StationCode, o.OriginStation.Name), new(o.DestinationStationId, o.DestinationStation.StationCode, o.DestinationStation.Name),
            e.FailureLogId, e.FailureType, e.FailureReason, e.FailedAt, e.ResolutionStatus, e.LastResolutionAction, e.ResolvedAt, SchedulingService.Utc(o.UpdatedAt)); }).ToList();
        return new(items, filter.Page, filter.PageSize, count, (int)Math.Ceiling(count / (double)filter.PageSize));
    }

    public async Task<FailureOrderDetailDto> DetailAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        await EnsureStaffAsync(actorId, false, ct);
        var order = await db.DeliveryOrders.AsNoTracking().Include(o => o.Package).Include(o => o.OriginStation).Include(o => o.DestinationStation)
            .Include(o => o.StatusLogs).ThenInclude(l => l.FailureEpisode).Include(o => o.StatusLogs).ThenInclude(l => l.HandlingEvents)
            .Include(o => o.FlightMissions).ThenInclude(m => m.Drone).AsSplitQuery().SingleOrDefaultAsync(o => o.Id == id, ct) ?? throw NotFound();
        var logs = order.StatusLogs.Where(l => l.ToStatus == "FAILED").OrderBy(l => l.CreatedAt).ThenBy(l => l.Id).ToList();
        if (logs.Count == 0) throw NotFound();
        var history = logs.Select(l => Episode(l, order)).ToList(); var latest = history[^1];
        var events = logs.SelectMany(l => l.HandlingEvents).OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToList();
        var latestEvents = logs[^1].HandlingEvents;
        var returnStatus = latestEvents.Any(e => e.Action == "RETURN_CONFIRMED") || (logs[^1].FailureEpisode is null && order.CancelReason == "RETURNED_TO_ORIGIN" && latest.LastResolutionAction == "RETURN_TO_ORIGIN") ? "CONFIRMED"
            : latest.LastResolutionAction == "RETURN_TO_ORIGIN" ? "REQUESTED" : "NOT_REQUESTED";
        // Occupancy and every ownership predicate must share one SQL statement/snapshot. In particular,
        // intake can atomically establish a newer source claim without changing mission creation order.
        var location = await db.FlightMissions.AsNoTracking()
            .Where(m => m.OrderId == id && m.DestinationSlotId != null && m.DestinationSlot!.Status == "OCCUPIED")
            .Where(m => !db.FlightMissions.Any(source => source.OrderId != id && source.StartSlotId == m.DestinationSlotId
                && source.FlightStatus == "PLANNED" && source.Order.Status == "READY_FOR_TAKEOFF"))
            .Where(m => db.FlightMissions.Where(claim => claim.StartSlotId == m.DestinationSlotId || claim.DestinationSlotId == m.DestinationSlotId)
                .OrderByDescending(claim => claim.CreatedAt).ThenByDescending(claim => claim.Id).Select(claim => claim.Id).FirstOrDefault() == m.Id)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Select(m => new ParcelLocationDto(m.DestinationSlot!.StationId, m.DestinationSlotId)).FirstOrDefaultAsync(ct);
        return new(DispatchOrderProjection.From(order), latest, returnStatus, location, history,
            events.Select(e => new FailureHandlingEventDto(e.FailureLogId, e.Action, e.Note, e.PerformedBy, SchedulingService.Utc(e.CreatedAt))).ToList(), logs.All(l => l.FailureEpisode != null));
    }

    public async Task<IReadOnlyList<ActiveDeliveryDto>> ActiveAsync(Guid actorId, Guid? stationId, CancellationToken ct = default)
    {
        await EnsureStaffAsync(actorId, true, ct);
        var missions = db.FlightMissions.AsNoTracking().Where(m => m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR");
        if (stationId.HasValue) missions = missions.Where(m => m.Order.OriginStationId == stationId || m.Order.DestinationStationId == stationId);
        var rows = await missions.OrderBy(m => m.Order.ScheduledDepartureTime).ThenBy(m => m.OrderId).Select(m => new {
            m.OrderId, m.Order.TrackingNumber, OrderStatus = m.Order.Status, m.Id, m.FlightStatus, m.Drone.SerialNumber,
            m.Order.OriginStationId, m.Order.DestinationStationId, m.Order.ScheduledDepartureTime, m.Order.EstimatedDeliveryTime,
            LastTelemetryAt = m.TelemetryLogs.OrderByDescending(t => t.Timestamp).ThenByDescending(t => t.Id).Select(t => (DateTime?)t.Timestamp).FirstOrDefault()
        }).ToListAsync(ct);
        var cutoff = time.GetUtcNow().UtcDateTime.AddSeconds(-60);
        return rows.Select(r => new ActiveDeliveryDto(r.OrderId, r.TrackingNumber, r.OrderStatus, r.Id, r.FlightStatus, r.SerialNumber,
            r.OriginStationId, r.DestinationStationId, SchedulingService.Utc(r.ScheduledDepartureTime), SchedulingService.Utc(r.EstimatedDeliveryTime),
            SchedulingService.Utc(r.LastTelemetryAt), r.LastTelemetryAt is null || r.LastTelemetryAt < cutoff)).ToList();
    }

    private static FailureEpisodeDto Episode(DeliveryStatusLog log, DeliveryOrder order)
    {
        var nextFailure = order.StatusLogs.Where(l => l.ToStatus == "FAILED" && After(l, log)).OrderBy(l => l.CreatedAt).ThenBy(l => l.Id).FirstOrDefault();
        var resolved = order.StatusLogs.Where(l => l.FromStatus == "FAILED" && l.ToStatus is "APPROVED" or "CANCELLED" && After(l, log) && (nextFailure == null || After(nextFailure, l))).OrderBy(l => l.CreatedAt).ThenBy(l => l.Id).FirstOrDefault();
        var events = log.HandlingEvents.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToList();
        var action = events.LastOrDefault(e => e.Action is "RESCHEDULE" or "RETURN_TO_ORIGIN" or "CANCEL")?.Action;
        var returned = events.Any(e => e.Action == "RETURN_CONFIRMED");
        // Structured episodes use durable events. Only genuinely legacy metadata may use old markers;
        // the same CancelReason column is also valid human-written intake rejection text.
        var returnRequested = nextFailure is null && order.Status == "FAILED" && log.FailureEpisode is null && order.CancelReason == "RETURN_REQUESTED";
        var returnConfirmed = nextFailure is null && log.FailureEpisode is null && order.CancelReason == "RETURNED_TO_ORIGIN" && resolved?.ToStatus == "CANCELLED";
        if (action is null && (returnRequested || returnConfirmed)) action = "RETURN_TO_ORIGIN";
        return new(log.Id, log.FailureEpisode?.FailureType ?? "UNKNOWN", log.FailureEpisode?.OriginalReason ?? log.Remarks,
            SchedulingService.Utc(log.CreatedAt), resolved != null || returned ? "RESOLVED" : returnRequested || action == "RETURN_TO_ORIGIN" ? "RETURN_REQUESTED" : "UNRESOLVED", action,
            SchedulingService.Utc(resolved?.CreatedAt ?? events.LastOrDefault(e => e.Action == "RETURN_CONFIRMED")?.CreatedAt));
    }
    private static bool After(DeliveryStatusLog a, DeliveryStatusLog b) => a.CreatedAt > b.CreatedAt || a.CreatedAt == b.CreatedAt && a.Id > b.Id;
    private async Task<bool> OwnsDestinationSlotAsync(FlightMission mission, Guid slotId, CancellationToken ct)
    {
        // Intake can claim a source long after assignment created its mission. A current
        // READY source claim is stronger evidence than any historical destination link.
        if (await db.FlightMissions.AsNoTracking().AnyAsync(m => m.OrderId != mission.OrderId && m.StartSlotId == slotId
            && m.FlightStatus == "PLANNED" && m.Order.Status == "READY_FOR_TAKEOFF", ct)) return false;
        return await MissionResources.IsLatestSlotClaimAsync(db, mission, slotId, ct);
    }
    private async Task<DeliveryOrder> LockOrderAsync(Guid id, CancellationToken ct) => (db.Database.IsNpgsql()
        ? (await db.DeliveryOrders.FromSqlInterpolated($"SELECT * FROM delivery_orders WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
        : await db.DeliveryOrders.SingleOrDefaultAsync(o => o.Id == id, ct)) ?? throw NotFound();
    private async Task EnsureStaffAsync(Guid id, bool manager, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.Status == "ACTIVE" && (u.Role.Code == AppRoles.Dispatcher || u.Role.Code == AppRoles.Admin || manager && u.Role.Code == AppRoles.Manager), ct))
            throw new ApiException(403, "An active authorized staff account is required.");
    }
    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); throw Conflict("The order changed concurrently."); }
    }
    private void Notify(Guid recipient, DeliveryOrder order, string title, string message, DateTime now) => db.Notifications.Add(new Notification { UserId = recipient, OrderId = order.Id, Type = "ORDER_STATUS", Title = title, Message = message, CreatedAt = now });
    private void Audit(DeliveryOrder order, Guid? actor, string action, DateTime now) => db.AuditLogs.Add(new AuditLog { UserId = actor, EntityName = "DeliveryOrder", EntityId = order.Id.ToString(), Action = action, Timestamp = now });
    private static void ValidateFlightType(string? type) { if (type is not ("MISSION_ABORTED" or "EMERGENCY_LANDING" or "HARDWARE_FAULT")) throw Field("failureType", "Unsupported flight failure type."); }
    private static string RequiredText(string? value, string field) { var text = value?.Trim(); return text is { Length: >= 1 and <= 500 } ? text : throw Field(field, "Use between 1 and 500 characters."); }
    private static OrderDecisionDto Decision(DeliveryOrder o) => new(o.Id, o.TrackingNumber, o.Status, SchedulingService.Utc(o.UpdatedAt));
    private static ApiException NotFound() => new(404, "Order or failure episode not found.");
    private static ApiException Conflict(string message) => new(409, message);
    private static ApiException Field(string name, string message) => new(400, "One or more fields are invalid.", new Dictionary<string, string[]> { [name] = [message] });
}
