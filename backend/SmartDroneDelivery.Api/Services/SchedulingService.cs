using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public interface ISchedulingService
{
    Task<ScheduleDto> ScheduleAsync(Guid orderId, Guid actorId, ScheduleOrderRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<DroneCandidateDto>> CandidatesAsync(Guid orderId, Guid actorId, CancellationToken ct = default);
    Task<FlightMissionDto> AssignAsync(Guid orderId, Guid actorId, AssignDroneRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);
    Task<PagedResult<ApprovedOrderDto>> ListApprovedAsync(Guid actorId, ApprovedOrderFilterQuery query, CancellationToken ct = default);
    Task<DispatchOrderDetailDto> DetailAsync(Guid orderId, Guid actorId, CancellationToken ct = default);
}

public class SchedulingService(AppDbContext db, IOrderTransitionWriter transitions, DeliveryRouteEstimate routes) : ISchedulingService
{
    public async Task<ScheduleDto> ScheduleAsync(Guid orderId, Guid actorId, ScheduleOrderRequest request, CancellationToken ct = default)
    {
        using var operation = await transitions.EnterOperationAsync(ct);
        await EnsureDispatcherAsync(actorId, ct);
        var departure = ParseUtc(request.ScheduledDepartureTime, "scheduledDepartureTime")
            ?? throw FieldError("scheduledDepartureTime", "A UTC departure timestamp is required.");
        if (departure < DateTime.UtcNow.AddMinutes(15)) throw FieldError("scheduledDepartureTime", "Departure must be at least 15 minutes in the future.");
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var order = await LockOrderAsync(orderId, ct);
        if (order.Status != "APPROVED" || await db.FlightMissions.AnyAsync(m => m.OrderId == orderId && (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR"), ct))
            throw Conflict("Only an APPROVED order without an active mission can be scheduled.");
        await LockStationsAsync(order, ct);
        var estimate = await routes.GetAsync(order, ct);
        DateTime arrival;
        try { arrival = departure.AddMinutes(estimate.FlightMinutes); }
        catch (ArgumentOutOfRangeException) { throw FieldError("scheduledDepartureTime", "Departure and estimated duration exceed the supported date range."); }
        order.ScheduledDepartureTime = departure;
        order.EstimatedDeliveryTime = arrival;
        order.UpdatedAt = DateTime.UtcNow;
        await SaveAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return new(order.Id, departure, arrival, estimate.FlightMinutes);
    }

    public async Task<IReadOnlyList<DroneCandidateDto>> CandidatesAsync(Guid orderId, Guid actorId, CancellationToken ct = default)
    {
        await EnsureDispatcherAsync(actorId, ct);
        var order = await db.DeliveryOrders.AsNoTracking().Include(o => o.Package).FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw NotFound("Delivery order");
        RequireScheduled(order);
        var estimate = await routes.GetAsync(order, ct);
        var drones = await db.Drones.AsNoTracking().ToListAsync(ct);
        var missions = await db.FlightMissions.AsNoTracking().Include(m => m.Order)
            .Where(m => m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR" || m.StartTime != null).ToListAsync(ct);
        return drones.Select(d => {
            var reasons = Reasons(d, order, estimate.DistanceKm, missions.Where(m => m.DroneId == d.Id));
            return new DroneCandidateDto(d.Id, d.SerialNumber, d.ModelName, d.MaxPayloadGram, d.MaxFlightRangeKm,
                d.CurrentStationId, d.Status, reasons.Count == 0, reasons);
        }).OrderByDescending(d => d.IsEligible).ThenBy(d => d.SerialNumber, StringComparer.Ordinal).ToList();
    }

    public async Task<FlightMissionDto> AssignAsync(Guid orderId, Guid actorId, AssignDroneRequest request,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        using var operation = await transitions.EnterOperationAsync(ct);
        await EnsureDispatcherAsync(actorId, ct);
        if (!request.DroneId.HasValue || request.DroneId == Guid.Empty) throw FieldError("droneId", "A drone UUID is required.");
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var order = await LockOrderAsync(orderId, ct);
        RequireScheduled(order);
        if (await db.FlightMissions.AnyAsync(m => m.OrderId == orderId && (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR"), ct))
            throw Conflict("The order already has an active mission.");
        var stations = await LockStationsAsync(order, ct);
        if (stations.Any(s => s.Status != "OPERATIONAL") || stations.Count != 2)
            throw Conflict("Both route stations must be OPERATIONAL.");
        await db.Entry(order).Reference(o => o.Package).LoadAsync(ct);
        var droneId = request.DroneId.Value;
        var drone = db.Database.IsNpgsql()
            ? (await db.Drones.FromSqlInterpolated($"SELECT * FROM drones WHERE \"Id\" = {droneId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            : await db.Drones.FirstOrDefaultAsync(d => d.Id == droneId, ct);
        if (drone is null) throw NotFound("Drone");
        var estimate = await routes.GetAsync(order, ct);
        var missions = await db.FlightMissions.AsNoTracking().Include(m => m.Order).Where(m => m.DroneId == drone.Id
            && (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR" || m.StartTime != null)).ToListAsync(ct);
        var reasons = Reasons(drone, order, estimate.DistanceKm, missions);
        if (reasons.Contains("PAYLOAD_EXCEEDED")) throw FieldError("droneId", "The package exceeds this drone's payload.");
        if (reasons.Contains("RANGE_INSUFFICIENT")) throw FieldError("droneId", "The route exceeds this drone's range including the 30 percent reserve.");
        if (reasons.Count != 0) throw Conflict("Drone is not eligible: " + string.Join(", ", reasons) + ".");
        // Status remains an independent safety check even if legacy mission intervals are missing.
        if (missions.Any(m => m.FlightStatus is "PLANNED" or "IN_AIR")) throw Conflict("Drone already has an active mission.");
        var slot = db.Database.IsNpgsql()
            ? (await db.StationSlots.FromSqlInterpolated($"SELECT * FROM station_slots WHERE \"StationId\" = {order.DestinationStationId} AND \"Status\" = 'EMPTY' ORDER BY \"SlotNumber\", \"Id\" LIMIT 1 FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            : await db.StationSlots.Where(s => s.StationId == order.DestinationStationId && s.Status == "EMPTY").OrderBy(s => s.SlotNumber).ThenBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (slot is null) throw Conflict("No EMPTY destination slot is available.");
        var now = DateTime.UtcNow;
        var mission = new FlightMission { OrderId = order.Id, DroneId = drone.Id, DestinationSlotId = slot.Id,
            CreatedAt = now, ScheduledDepartureTime = order.ScheduledDepartureTime, EstimatedArrivalTime = order.EstimatedDeliveryTime };
        db.FlightMissions.Add(mission);
        drone.Status = "ASSIGNED";
        slot.Status = "RESERVED";
        slot.LastUpdated = now;
        order.UpdatedAt = now;
        db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "DRONE_ASSIGNMENT", EntityName = "DeliveryOrder",
            EntityId = order.Id.ToString(), IpAddress = ipAddress, UserAgent = userAgent, Timestamp = now });
        await SaveAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return new(mission.Id, order.Id, drone.Id, mission.FlightStatus, slot.Id, Utc(order.ScheduledDepartureTime!.Value));
    }

    public async Task<PagedResult<ApprovedOrderDto>> ListApprovedAsync(Guid actorId, ApprovedOrderFilterQuery query, CancellationToken ct = default)
    {
        await EnsureDispatcherAsync(actorId, ct);
        if (query.Page < 1) throw FieldError("page", "Page must be at least 1.");
        if (query.PageSize is < 1 or > 100) throw FieldError("pageSize", "Page size must be 1 to 100.");
        var keyword = query.SearchKeyword?.Trim();
        if (keyword?.Length > 50) throw FieldError("searchKeyword", "Search keyword must be at most 50 characters.");
        if (query.AssignmentStatus is not (null or "ASSIGNED" or "UNASSIGNED")) throw FieldError("assignmentStatus", "Assignment status must be ASSIGNED or UNASSIGNED.");
        var from = ParseUtc(query.FromDate, "fromDate"); var to = ParseUtc(query.ToDate, "toDate");
        var scheduledFrom = ParseUtc(query.ScheduledFrom, "scheduledFrom"); var scheduledTo = ParseUtc(query.ScheduledTo, "scheduledTo");
        if (from > to) throw FieldError("toDate", "To date must be on or after from date.");
        if (scheduledFrom > scheduledTo) throw FieldError("scheduledTo", "Scheduled to must be on or after scheduled from.");
        var orders = db.DeliveryOrders.AsNoTracking().Where(o => o.Status == "APPROVED");
        if (query.OriginStationId.HasValue) orders = orders.Where(o => o.OriginStationId == query.OriginStationId);
        if (query.DestinationStationId.HasValue) orders = orders.Where(o => o.DestinationStationId == query.DestinationStationId);
        if (!string.IsNullOrEmpty(keyword)) { var term = keyword.ToLowerInvariant(); orders = orders.Where(o => o.TrackingNumber.ToLower().Contains(term)); }
        if (from.HasValue) orders = orders.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue) orders = orders.Where(o => o.CreatedAt <= to.Value);
        if (scheduledFrom.HasValue) orders = orders.Where(o => o.ScheduledDepartureTime >= scheduledFrom.Value);
        if (scheduledTo.HasValue) orders = orders.Where(o => o.ScheduledDepartureTime <= scheduledTo.Value);
        if (query.AssignmentStatus == "ASSIGNED") orders = orders.Where(o => o.FlightMissions.Any(m => m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR"));
        if (query.AssignmentStatus == "UNASSIGNED") orders = orders.Where(o => !o.FlightMissions.Any(m => m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR"));
        var count = await orders.CountAsync(ct);
        var offset = ((long)query.Page - 1) * query.PageSize;
        var page = offset >= count ? [] : await orders.OrderBy(o => o.CreatedAt).ThenBy(o => o.Id).Skip((int)offset).Take(query.PageSize)
            .Include(o => o.Package).Include(o => o.OriginStation).Include(o => o.DestinationStation)
            .Include(o => o.FlightMissions.Where(m => m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR")).ThenInclude(m => m.Drone).ToListAsync(ct);
        var items = page.Select(o => {
            var mission = o.FlightMissions.SingleOrDefault();
            return new ApprovedOrderDto(o.Id, o.TrackingNumber, o.Status, o.Package.Id, o.Package.PackageName, o.Package.WeightGram,
                new(o.OriginStationId, o.OriginStation.StationCode, o.OriginStation.Name), new(o.DestinationStationId, o.DestinationStation.StationCode, o.DestinationStation.Name),
                Utc(o.ScheduledDepartureTime), Utc(o.EstimatedDeliveryTime), mission is null ? "UNASSIGNED" : "ASSIGNED", mission?.Id, mission?.DroneId,
                mission?.Drone.SerialNumber, Utc(o.CreatedAt), Utc(o.UpdatedAt));
        }).ToList();
        return new(items, query.Page, query.PageSize, count, (int)Math.Ceiling(count / (double)query.PageSize));
    }

    public async Task<DispatchOrderDetailDto> DetailAsync(Guid orderId, Guid actorId, CancellationToken ct = default)
    {
        await EnsureDispatcherAsync(actorId, ct);
        var order = await db.DeliveryOrders.AsNoTracking().Include(o => o.Package).Include(o => o.OriginStation).Include(o => o.DestinationStation)
            .Include(o => o.StatusLogs).Include(o => o.FlightMissions.Where(m => m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR")).ThenInclude(m => m.Drone)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct) ?? throw NotFound("Delivery order");
        return DispatchOrderProjection.From(order);
    }

    private async Task<DeliveryOrder> LockOrderAsync(Guid id, CancellationToken ct) =>
        (db.Database.IsNpgsql()
            ? (await db.DeliveryOrders.FromSqlInterpolated($"SELECT * FROM delivery_orders WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            : await db.DeliveryOrders.FirstOrDefaultAsync(o => o.Id == id, ct)) ?? throw NotFound("Delivery order");

    private Task<List<LandingStation>> LockStationsAsync(DeliveryOrder order, CancellationToken ct) => db.Database.IsNpgsql()
        ? db.LandingStations.FromSqlInterpolated($"SELECT * FROM landing_stations WHERE \"Id\" IN ({order.OriginStationId}, {order.DestinationStationId}) ORDER BY \"Id\" FOR SHARE").AsNoTracking().ToListAsync(ct)
        : db.LandingStations.AsNoTracking().Where(s => s.Id == order.OriginStationId || s.Id == order.DestinationStationId).ToListAsync(ct);

    private async Task EnsureDispatcherAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.Status == "ACTIVE" && (u.Role.Code == AppRoles.Dispatcher || u.Role.Code == AppRoles.Admin), ct))
            throw new ApiException(StatusCodes.Status403Forbidden, "An active dispatcher or admin account is required.");
    }

    private static void RequireScheduled(DeliveryOrder order)
    {
        if (order.Status != "APPROVED" || !order.ScheduledDepartureTime.HasValue || !order.EstimatedDeliveryTime.HasValue)
            throw Conflict("The order must be APPROVED and scheduled.");
    }

    private static List<string> Reasons(Drone drone, DeliveryOrder order, decimal distanceKm, IEnumerable<FlightMission> missions)
    {
        var reasons = new List<string>();
        if (drone.Status != "IDLE") reasons.Add("NOT_IDLE");
        if (order.Package.WeightGram > drone.MaxPayloadGram) reasons.Add("PAYLOAD_EXCEEDED");
        if (distanceKm * 1.30m > drone.MaxFlightRangeKm) reasons.Add("RANGE_INSUFFICIENT");
        var departure = order.ScheduledDepartureTime!.Value;
        var arrival = order.EstimatedDeliveryTime!.Value;
        if (missions.Any(m => Overlaps(m, departure, arrival))) reasons.Add("TIME_OVERLAP");
        if (drone.CurrentStationId != order.OriginStationId) reasons.Add("NOT_AT_ORIGIN");
        return reasons;
    }

    private static bool Overlaps(FlightMission mission, DateTime departure, DateTime arrival)
    {
        // Never project an order's new schedule onto its completed/aborted history.
        // Unflown aborted missions released their reservation and have no occupied interval.
        if (mission.FlightStatus == "ABORTED" && mission.StartTime is null) return false;
        var active = mission.FlightStatus is "PLANNED" or "IN_AIR";
        var start = mission.StartTime ?? mission.ScheduledDepartureTime ?? (active ? mission.Order.ScheduledDepartureTime : null);
        var end = mission.EndTime ?? mission.EstimatedArrivalTime ?? (active ? mission.Order.EstimatedDeliveryTime : null);
        if (start is null || end is null) return active; // incomplete active legacy mission is unsafe
        // Both closed windows include preparation/turnaround. Buffering only the
        // requested mission would make eligibility depend on assignment order.
        return AddClamped(start.Value, -15) <= AddClamped(arrival, 20)
            && AddClamped(end.Value, 20) >= AddClamped(departure, -15);
    }

    private static DateTime AddClamped(DateTime date, int minutes) => minutes > 0 && date > DateTime.MaxValue.AddMinutes(-minutes)
        ? DateTime.MaxValue : minutes < 0 && date < DateTime.MinValue.AddMinutes(-minutes) ? DateTime.MinValue : date.AddMinutes(minutes);

    public static DateTime? ParseUtc(string? value, string field)
    {
        if (value is null) return null;
        if (!value.Contains('T') || !(value.EndsWith('Z') || value.EndsWith("+00:00", StringComparison.Ordinal))
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) || parsed.Offset != TimeSpan.Zero)
            throw FieldError(field, "Use an ISO 8601 UTC timestamp with Z or +00:00.");
        return parsed.UtcDateTime;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); throw Conflict("The order or resource changed concurrently."); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { db.ChangeTracker.Clear(); throw Conflict("An active mission or resource was assigned concurrently."); }
    }

    internal static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    internal static DateTime? Utc(DateTime? value) => value.HasValue ? Utc(value.Value) : null;
    private static ApiException NotFound(string entity) => new(StatusCodes.Status404NotFound, entity + " not found.");
    private static ApiException Conflict(string detail) => new(StatusCodes.Status409Conflict, detail);
    private static ApiException FieldError(string field, string message) => new(StatusCodes.Status400BadRequest, "One or more fields are invalid.", new Dictionary<string, string[]> { [field] = [message] });
}

public static class DispatchOrderProjection
{
    public static DispatchOrderDetailDto From(DeliveryOrder order)
    {
        var m = order.FlightMissions.SingleOrDefault(m => m.FlightStatus is "PLANNED" or "IN_AIR");
        var p = order.Package;
        return new(order.Id, order.TrackingNumber, order.Status,
            new(p.Id, p.SenderId, p.PackageName, p.WeightGram, p.LengthCm, p.WidthCm, p.HeightCm, p.PackageImageUrl, p.IsFragile, SchedulingService.Utc(p.CreatedAt)),
            StationSummaryDto.From(order.OriginStation), StationSummaryDto.From(order.DestinationStation),
            SchedulingService.Utc(order.ScheduledDepartureTime), SchedulingService.Utc(order.EstimatedDeliveryTime),
            SchedulingService.Utc(order.ActualDepartureTime), SchedulingService.Utc(order.ActualDeliveryTime), m is null ? "UNASSIGNED" : "ASSIGNED",
            m is null ? null : new(m.Id, m.FlightStatus, new(m.Drone.Id, m.Drone.SerialNumber, m.Drone.ModelName, m.Drone.Status),
                m.StartSlotId, m.DestinationSlotId, SchedulingService.Utc(m.StartTime), SchedulingService.Utc(m.EndTime)),
            order.StatusLogs.OrderBy(l => l.CreatedAt).ThenBy(l => l.Id).Select(l => new StatusLogDto(l.FromStatus, l.ToStatus, l.Remarks, SchedulingService.Utc(l.CreatedAt))).ToList(),
            SchedulingService.Utc(order.CreatedAt), SchedulingService.Utc(order.UpdatedAt));
    }
}
