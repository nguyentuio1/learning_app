using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public interface IStationSlotService
{
    Task<IReadOnlyList<StationSlotDto>> ListByStationAsync(Guid userId, Guid stationId);
    Task<StationSlotDto> GetByIdAsync(Guid userId, Guid id);
    Task<StationSlotDto> AddSlotAsync(Guid userId, Guid stationId, AddStationSlotRequest? request);
    Task RemoveSlotAsync(Guid userId, Guid id);
    Task<StationSlotDto> UpdateStatusAsync(Guid userId, Guid id, UpdateStationSlotStatusRequest request);
}

public class StationSlotService : IStationSlotService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> StationLocks = new();
    private readonly AppDbContext _db;

    public StationSlotService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<StationSlotDto>> ListByStationAsync(Guid userId, Guid stationId)
    {
        await LoadActiveUserAsync(userId);

        var stationExists = await _db.LandingStations.AsNoTracking().AnyAsync(s => s.Id == stationId);
        if (!stationExists)
            throw new ApiException(StatusCodes.Status404NotFound, "Landing station not found.");

        var slots = await _db.StationSlots
            .AsNoTracking()
            .Where(s => s.StationId == stationId)
            .OrderBy(s => s.SlotNumber)
            .ToListAsync();

        var slotIds = slots.Select(s => s.Id).ToList();
        var activeMissions = slotIds.Count == 0
            ? new List<FlightMission>()
            : await _db.FlightMissions
                .AsNoTracking()
                .Where(m => (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR") &&
                            ((m.StartSlotId.HasValue && slotIds.Contains(m.StartSlotId.Value)) ||
                             (m.DestinationSlotId.HasValue && slotIds.Contains(m.DestinationSlotId.Value))))
                .ToListAsync();

        return slots.Select(slot => MapSlot(slot, activeMissions)).ToList();
    }

    public async Task<StationSlotDto> GetByIdAsync(Guid userId, Guid id)
    {
        await LoadActiveUserAsync(userId);

        var slot = await _db.StationSlots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Station slot not found.");

        var activeMissions = await _db.FlightMissions
            .AsNoTracking()
            .Where(m => (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR") &&
                        (m.StartSlotId == id || m.DestinationSlotId == id))
            .ToListAsync();

        return MapSlot(slot, activeMissions);
    }

    public async Task<StationSlotDto> AddSlotAsync(Guid userId, Guid stationId, AddStationSlotRequest? request)
    {
        await LoadActiveUserAsync(userId);

        if (request?.SlotNumber is < 1)
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["slotNumber"] = ["Slot number must be at least 1."]
                });
        }

        var sem = StationLocks.GetOrAdd(stationId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            if (_db.Database.IsRelational())
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

                // Exclusive row lock on landing_stations in PostgreSQL
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT id FROM landing_stations WHERE id = {stationId} FOR UPDATE");

                var station = await _db.LandingStations
                    .Include(s => s.Slots)
                    .FirstOrDefaultAsync(s => s.Id == stationId)
                    ?? throw new ApiException(StatusCodes.Status404NotFound, "Landing station not found.");

                var existingCount = station.Slots.Count;
                if (existingCount >= 20)
                    throw new ApiException(StatusCodes.Status409Conflict, "Station already has the maximum of 20 slots.");

                var nextSlotNumber = request?.SlotNumber
                    ?? (existingCount == 0 ? 1 : station.Slots.Max(s => s.SlotNumber) + 1);

                if (station.Slots.Any(s => s.SlotNumber == nextSlotNumber))
                    throw new ApiException(StatusCodes.Status409Conflict, "Slot number is already used in this station.");

                var now = DateTime.UtcNow;
                var slot = new StationSlot
                {
                    Id = Guid.NewGuid(),
                    StationId = stationId,
                    SlotNumber = nextSlotNumber,
                    Status = "EMPTY",
                    LastUpdated = now
                };

                _db.StationSlots.Add(slot);
                station.TotalSlots = existingCount + 1;
                station.UpdatedAt = now;

                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "SLOT_CREATE",
                    EntityName = "StationSlot",
                    EntityId = slot.Id.ToString(),
                    Timestamp = now
                });

                try
                {
                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch (DbUpdateException)
                {
                    await tx.RollbackAsync();
                    throw new ApiException(StatusCodes.Status409Conflict, "Slot number is already used in this station.");
                }

                return MapSlot(slot, Array.Empty<FlightMission>());
            }
            else
            {
                var station = await _db.LandingStations
                    .Include(s => s.Slots)
                    .FirstOrDefaultAsync(s => s.Id == stationId)
                    ?? throw new ApiException(StatusCodes.Status404NotFound, "Landing station not found.");

                var existingCount = station.Slots.Count;
                if (existingCount >= 20)
                    throw new ApiException(StatusCodes.Status409Conflict, "Station already has the maximum of 20 slots.");

                var nextSlotNumber = request?.SlotNumber
                    ?? (existingCount == 0 ? 1 : station.Slots.Max(s => s.SlotNumber) + 1);

                if (station.Slots.Any(s => s.SlotNumber == nextSlotNumber))
                    throw new ApiException(StatusCodes.Status409Conflict, "Slot number is already used in this station.");

                var now = DateTime.UtcNow;
                var slot = new StationSlot
                {
                    Id = Guid.NewGuid(),
                    StationId = stationId,
                    SlotNumber = nextSlotNumber,
                    Status = "EMPTY",
                    LastUpdated = now
                };

                _db.StationSlots.Add(slot);
                station.TotalSlots = existingCount + 1;
                station.UpdatedAt = now;

                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "SLOT_CREATE",
                    EntityName = "StationSlot",
                    EntityId = slot.Id.ToString(),
                    Timestamp = now
                });

                await _db.SaveChangesAsync();
                return MapSlot(slot, Array.Empty<FlightMission>());
            }
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task RemoveSlotAsync(Guid userId, Guid id)
    {
        await LoadActiveUserAsync(userId);

        var initialSlot = await _db.StationSlots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Station slot not found.");

        var sem = StationLocks.GetOrAdd(initialSlot.StationId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            if (_db.Database.IsRelational())
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

                // Exclusive row lock on landing_stations in PostgreSQL
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT id FROM landing_stations WHERE id = {initialSlot.StationId} FOR UPDATE");

                var slot = await _db.StationSlots
                    .Include(s => s.Station)
                    .ThenInclude(st => st.Slots)
                    .FirstOrDefaultAsync(s => s.Id == id)
                    ?? throw new ApiException(StatusCodes.Status404NotFound, "Station slot not found.");

                if (slot.Status is "OCCUPIED" or "RESERVED")
                    throw new ApiException(StatusCodes.Status409Conflict, "Slot is occupied or reserved and cannot be removed.");

                var hasActiveMission = await _db.FlightMissions
                    .AsNoTracking()
                    .AnyAsync(m => (m.StartSlotId == id || m.DestinationSlotId == id) &&
                                   (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR"));
                if (hasActiveMission)
                    throw new ApiException(StatusCodes.Status409Conflict, "Slot is linked to a planned or in-air mission and cannot be removed.");

                var existingCount = slot.Station.Slots.Count;
                if (existingCount <= 1)
                    throw new ApiException(StatusCodes.Status409Conflict, "Cannot remove the last slot of a station.");

                var now = DateTime.UtcNow;
                _db.StationSlots.Remove(slot);
                slot.Station.TotalSlots = existingCount - 1;
                slot.Station.UpdatedAt = now;

                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "SLOT_DELETE",
                    EntityName = "StationSlot",
                    EntityId = slot.Id.ToString(),
                    Timestamp = now
                });

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            else
            {
                var slot = await _db.StationSlots
                    .Include(s => s.Station)
                    .ThenInclude(st => st.Slots)
                    .FirstOrDefaultAsync(s => s.Id == id)
                    ?? throw new ApiException(StatusCodes.Status404NotFound, "Station slot not found.");

                if (slot.Status is "OCCUPIED" or "RESERVED")
                    throw new ApiException(StatusCodes.Status409Conflict, "Slot is occupied or reserved and cannot be removed.");

                var hasActiveMission = await _db.FlightMissions
                    .AsNoTracking()
                    .AnyAsync(m => (m.StartSlotId == id || m.DestinationSlotId == id) &&
                                   (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR"));
                if (hasActiveMission)
                    throw new ApiException(StatusCodes.Status409Conflict, "Slot is linked to a planned or in-air mission and cannot be removed.");

                var existingCount = slot.Station.Slots.Count;
                if (existingCount <= 1)
                    throw new ApiException(StatusCodes.Status409Conflict, "Cannot remove the last slot of a station.");

                var now = DateTime.UtcNow;
                _db.StationSlots.Remove(slot);
                slot.Station.TotalSlots = existingCount - 1;
                slot.Station.UpdatedAt = now;

                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "SLOT_DELETE",
                    EntityName = "StationSlot",
                    EntityId = slot.Id.ToString(),
                    Timestamp = now
                });

                await _db.SaveChangesAsync();
            }
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<StationSlotDto> UpdateStatusAsync(Guid userId, Guid id, UpdateStationSlotStatusRequest request)
    {
        var caller = await LoadActiveUserAsync(userId);

        var slot = await _db.StationSlots
            .Include(s => s.Station)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Station slot not found.");

        if (caller.Role.Code == AppRoles.StationOperator && slot.Station.OperatorId != userId)
            throw new ApiException(StatusCodes.Status403Forbidden, "You can only manage slots at your assigned station.");

        var errors = new Dictionary<string, List<string>>();
        var targetStatus = request.TargetStatus?.Trim() ?? string.Empty;
        if (targetStatus is not ("EMPTY" or "ERROR"))
            AddError(errors, "targetStatus", "Target status must be EMPTY or ERROR.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 5 or > 500)
            AddError(errors, "reason", "Reason is required and must be between 5 and 500 characters.");

        ThrowIfInvalid(errors);

        if (slot.Status is "OCCUPIED" or "RESERVED")
            throw new ApiException(StatusCodes.Status409Conflict, "Slot is occupied or reserved and cannot be set to maintenance.");

        if (slot.Status == targetStatus)
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["targetStatus"] = [targetStatus == "EMPTY" ? "The slot is not in maintenance." : "The slot is already in maintenance."]
                });
        }

        var currentStatus = slot.Status;
        var now = DateTime.UtcNow;

        var audit = new AuditLog
        {
            UserId = userId,
            Action = "SLOT_STATUS_CHANGE",
            EntityName = "StationSlot",
            EntityId = slot.Id.ToString(),
            Timestamp = now
        };

        if (_db.Database.IsRelational())
        {
            var updatedRows = await _db.StationSlots
                .Where(s => s.Id == id && s.Status == currentStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, targetStatus)
                    .SetProperty(x => x.LastUpdated, now));

            if (updatedRows == 0)
            {
                throw new ApiException(StatusCodes.Status409Conflict,
                    "The slot status was updated concurrently. Refresh and try again.");
            }

            _db.AuditLogs.Add(audit);
            await _db.SaveChangesAsync();
            slot.Status = targetStatus;
            slot.LastUpdated = now;
        }
        else
        {
            slot.Status = targetStatus;
            slot.LastUpdated = now;
            _db.AuditLogs.Add(audit);
            await _db.SaveChangesAsync();
        }

        return MapSlot(slot, Array.Empty<FlightMission>());
    }

    private static StationSlotDto MapSlot(StationSlot slot, IReadOnlyCollection<FlightMission> activeMissions)
    {
        Guid? currentOrderId = null;
        Guid? currentMissionId = null;
        if (slot.Status is "OCCUPIED" or "RESERVED")
        {
            var mission = activeMissions.FirstOrDefault(m =>
                m.StartSlotId == slot.Id || m.DestinationSlotId == slot.Id);
            if (mission is not null)
            {
                currentOrderId = mission.OrderId;
                currentMissionId = mission.Id;
            }
        }

        return new StationSlotDto(
            slot.Id,
            slot.StationId,
            slot.SlotNumber,
            slot.Status,
            DateTime.SpecifyKind(slot.LastUpdated, DateTimeKind.Utc),
            currentOrderId,
            currentMissionId);
    }

    private async Task<User> LoadActiveUserAsync(Guid userId)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");

        if (user.Status != "ACTIVE")
            throw new ApiException(StatusCodes.Status403Forbidden, "This account is not active.");

        return user;
    }

    private static void AddError(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var list))
        {
            list = new List<string>();
            errors[field] = list;
        }
        list.Add(message);
    }

    private static void ThrowIfInvalid(Dictionary<string, List<string>> errors)
    {
        if (errors.Count > 0)
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()));
        }
    }
}
