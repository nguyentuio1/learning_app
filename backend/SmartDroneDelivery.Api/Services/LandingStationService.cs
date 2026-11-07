using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public interface ILandingStationService
{
    Task<IReadOnlyList<LandingStationDto>> ListAsync(Guid userId, string? status, string? search);
    Task<LandingStationDetailDto> GetByIdAsync(Guid userId, Guid id);
    Task<LandingStationDto> CreateAsync(Guid userId, CreateLandingStationRequest request);
    Task<LandingStationDto> UpdateAsync(Guid userId, Guid id, UpdateLandingStationRequest request);
    Task<LandingStationStatusChangeResponseDto> ChangeStatusAsync(Guid userId, Guid id, ChangeLandingStationStatusRequest request);
}

public partial class LandingStationService : ILandingStationService
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.Ordinal)
    {
        "OPERATIONAL", "MAINTENANCE", "OFFLINE"
    };

    [GeneratedRegex(@"^[A-Z0-9-]{3,50}$")]
    private static partial Regex StationCodeRegex();

    private readonly AppDbContext _db;

    public LandingStationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<LandingStationDto>> ListAsync(Guid userId, string? status, string? search)
    {
        await LoadActiveUserAsync(userId);

        var query = _db.LandingStations
            .AsNoTracking()
            .Include(s => s.Operator)
            .Include(s => s.Slots)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = status.Trim().ToUpperInvariant();
            if (!ValidStatuses.Contains(wanted))
            {
                throw new ApiException(StatusCodes.Status400BadRequest, "One or more validation errors occurred.",
                    new Dictionary<string, string[]>
                    {
                        ["status"] = ["Status must be OPERATIONAL, MAINTENANCE, or OFFLINE."]
                    });
            }

            query = query.Where(s => s.Status == wanted);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.StationCode.ToLower().Contains(term) ||
                s.Name.ToLower().Contains(term));
        }

        var stations = await query.OrderBy(s => s.Name).ToListAsync();
        return stations.Select(LandingStationDto.From).ToList();
    }

    public async Task<LandingStationDetailDto> GetByIdAsync(Guid userId, Guid id)
    {
        var caller = await LoadActiveUserAsync(userId);

        var station = await _db.LandingStations
            .AsNoTracking()
            .Include(s => s.Operator)
            .Include(s => s.Slots)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Landing station not found.");

        if (caller.Role?.Code == AppRoles.StationOperator && station.OperatorId != userId)
        {
            throw new ApiException(StatusCodes.Status403Forbidden, "You can only view your assigned landing station.");
        }

        var slotIds = station.Slots.Select(s => s.Id).ToList();
        var activeMissions = slotIds.Count == 0
            ? new List<FlightMission>()
            : await _db.FlightMissions
                .AsNoTracking()
                .Where(m => (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR") &&
                            ((m.StartSlotId.HasValue && slotIds.Contains(m.StartSlotId.Value)) ||
                             (m.DestinationSlotId.HasValue && slotIds.Contains(m.DestinationSlotId.Value))))
                .ToListAsync();

        var hostedDroneCount = await _db.Drones
            .AsNoTracking()
            .CountAsync(d => d.CurrentStationId == id);

        var slots = station.Slots
            .OrderBy(s => s.SlotNumber)
            .Select(slot =>
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

                return new LandingStationSlotDto(
                    slot.Id,
                    slot.StationId,
                    slot.SlotNumber,
                    slot.Status,
                    DateTime.SpecifyKind(slot.LastUpdated, DateTimeKind.Utc),
                    currentOrderId,
                    currentMissionId);
            })
            .ToList();

        var summary = LandingStationDto.From(station);
        return new LandingStationDetailDto(
            summary.Id,
            summary.StationCode,
            summary.Name,
            summary.Address,
            summary.Latitude,
            summary.Longitude,
            summary.Status,
            summary.TotalSlots,
            summary.EmptySlots,
            summary.OperatorId,
            summary.OperatorName,
            summary.CreatedAt,
            summary.UpdatedAt,
            slots,
            hostedDroneCount);
    }

    public async Task<LandingStationDto> CreateAsync(Guid userId, CreateLandingStationRequest request)
    {
        await LoadActiveUserAsync(userId);

        var errors = new Dictionary<string, List<string>>();
        var code = request.StationCode?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(code))
            AddError(errors, "stationCode", "Station code is required.");
        else if (!StationCodeRegex().IsMatch(code))
            AddError(errors, "stationCode", "Station code must be 3 to 50 uppercase letters, digits, or hyphens.");

        ValidateCommonFields(request.Name, request.Address, request.Latitude, request.Longitude, errors);

        if (!request.TotalSlots.HasValue)
            AddError(errors, "totalSlots", "Total slots is required.");
        else if (request.TotalSlots.Value is < 1 or > 20)
            AddError(errors, "totalSlots", "Total slots must be between 1 and 20.");

        User? assignedOperator = null;
        if (request.OperatorId.HasValue)
        {
            assignedOperator = await ValidateOperatorAsync(request.OperatorId.Value, errors);
        }

        ThrowIfInvalid(errors);

        if (await _db.LandingStations.AnyAsync(s => s.StationCode == code))
            throw new ApiException(StatusCodes.Status409Conflict, "Station code is already in use.");

        var now = DateTime.UtcNow;
        var totalSlots = request.TotalSlots!.Value;
        var station = new LandingStation
        {
            Id = Guid.NewGuid(),
            StationCode = code,
            Name = request.Name!.Trim(),
            Address = request.Address!.Trim(),
            Latitude = request.Latitude!.Value,
            Longitude = request.Longitude!.Value,
            TotalSlots = totalSlots,
            Status = "OPERATIONAL",
            OperatorId = assignedOperator?.Id,
            Operator = assignedOperator,
            CreatedAt = now,
            UpdatedAt = now
        };

        for (var i = 1; i <= totalSlots; i++)
        {
            station.Slots.Add(new StationSlot
            {
                Id = Guid.NewGuid(),
                StationId = station.Id,
                SlotNumber = i,
                Status = "EMPTY",
                LastUpdated = now
            });
        }

        var audit = new AuditLog
        {
            UserId = userId,
            Action = "STATION_CREATE",
            EntityName = "LandingStation",
            EntityId = station.Id.ToString(),
            Timestamp = now
        };

        if (_db.Database.IsRelational())
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            _db.LandingStations.Add(station);
            _db.AuditLogs.Add(audit);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        else
        {
            _db.LandingStations.Add(station);
            _db.AuditLogs.Add(audit);
            await _db.SaveChangesAsync();
        }

        return LandingStationDto.From(station);
    }

    public async Task<LandingStationDto> UpdateAsync(Guid userId, Guid id, UpdateLandingStationRequest request)
    {
        await LoadActiveUserAsync(userId);

        var station = await _db.LandingStations
            .Include(s => s.Operator)
            .Include(s => s.Slots)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Landing station not found.");

        var errors = new Dictionary<string, List<string>>();
        ValidateCommonFields(request.Name, request.Address, request.Latitude, request.Longitude, errors);

        User? assignedOperator = null;
        if (request.OperatorId.HasValue)
        {
            assignedOperator = await ValidateOperatorAsync(request.OperatorId.Value, errors);
        }

        ThrowIfInvalid(errors);

        var now = DateTime.UtcNow;
        station.Name = request.Name!.Trim();
        station.Address = request.Address!.Trim();
        station.Latitude = request.Latitude!.Value;
        station.Longitude = request.Longitude!.Value;
        station.OperatorId = assignedOperator?.Id;
        station.Operator = assignedOperator;
        station.UpdatedAt = now;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "STATION_UPDATE",
            EntityName = "LandingStation",
            EntityId = station.Id.ToString(),
            Timestamp = now
        });

        await _db.SaveChangesAsync();
        return LandingStationDto.From(station);
    }

    public async Task<LandingStationStatusChangeResponseDto> ChangeStatusAsync(
        Guid userId, Guid id, ChangeLandingStationStatusRequest request)
    {
        var caller = await LoadActiveUserAsync(userId);

        var station = await _db.LandingStations
            .Include(s => s.Operator)
            .Include(s => s.Slots)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Landing station not found.");

        if (caller.Role.Code == AppRoles.StationOperator && station.OperatorId != userId)
            throw new ApiException(StatusCodes.Status403Forbidden, "You can only manage your assigned station.");

        var errors = new Dictionary<string, List<string>>();
        var targetStatus = request.NewStatus?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(targetStatus) || !ValidStatuses.Contains(targetStatus))
            AddError(errors, "newStatus", "New status must be OPERATIONAL, MAINTENANCE, or OFFLINE.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 5 or > 500)
            AddError(errors, "reason", "Reason is required and must be between 5 and 500 characters.");

        ThrowIfInvalid(errors);

        if (station.Status == targetStatus)
        {
            return new LandingStationStatusChangeResponseDto(
                LandingStationDto.From(station),
                false,
                Array.Empty<Guid>());
        }

        var now = DateTime.UtcNow;
        var initialStatus = station.Status;

        await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;

        if (_db.Database.IsRelational())
        {
            var updatedRows = await _db.LandingStations
                .Where(s => s.Id == id && s.Status == initialStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, targetStatus)
                    .SetProperty(x => x.UpdatedAt, now));

            if (updatedRows == 0)
            {
                throw new ApiException(StatusCodes.Status409Conflict,
                    "The station status was updated concurrently. Refresh and try again.");
            }

            _db.Entry(station).State = EntityState.Unchanged;
        }
        else
        {
            if (station.Status != initialStatus)
            {
                throw new ApiException(StatusCodes.Status409Conflict,
                    "The station status was updated concurrently. Refresh and try again.");
            }
            station.Status = targetStatus;
            station.UpdatedAt = now;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "STATION_STATUS_CHANGE",
            EntityName = "LandingStation",
            EntityId = station.Id.ToString(),
            Timestamp = now
        });

        var affectedMissionIds = new List<Guid>();
        if (targetStatus is "MAINTENANCE" or "OFFLINE")
        {
            var inAirMissions = await _db.FlightMissions
                .AsNoTracking()
                .Include(m => m.Order)
                .Include(m => m.DestinationSlot)
                .Where(m => m.FlightStatus == "IN_AIR" &&
                            ((m.DestinationSlot != null && m.DestinationSlot.StationId == id) ||
                             (m.Order != null && m.Order.DestinationStationId == id)))
                .ToListAsync();

            affectedMissionIds = inAirMissions.Select(m => m.Id).Distinct().ToList();
            if (affectedMissionIds.Count > 0)
            {
                var dispatchers = await _db.Users
                    .Include(u => u.Role)
                    .Where(u => u.Status == "ACTIVE" && u.Role.Code == AppRoles.Dispatcher)
                    .Select(u => u.Id)
                    .ToListAsync();

                foreach (var dispatcherId in dispatchers)
                {
                    _db.Notifications.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = dispatcherId,
                        OrderId = inAirMissions[0].OrderId,
                        Type = "STATION_REROUTE_ALERT",
                        Title = $"Station {station.StationCode} set to {targetStatus}",
                        Message = $"Station {station.Name} ({station.StationCode}) changed to {targetStatus} while {affectedMissionIds.Count} flight(s) are in the air. Reason: {reason}",
                        CreatedAt = now
                    });
                }
            }
        }

        await _db.SaveChangesAsync();
        if (tx != null)
        {
            await tx.CommitAsync();
        }

        station.Status = targetStatus;
        station.UpdatedAt = now;

        return new LandingStationStatusChangeResponseDto(
            LandingStationDto.From(station),
            affectedMissionIds.Count > 0,
            affectedMissionIds);
    }

    private static void ValidateCommonFields(
        string? name, string? address, decimal? latitude, decimal? longitude,
        Dictionary<string, List<string>> errors)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedName))
            AddError(errors, "name", "Station name is required.");
        else if (trimmedName.Length > 150)
            AddError(errors, "name", "Station name must be at most 150 characters.");

        var trimmedAddress = address?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedAddress))
            AddError(errors, "address", "Address is required.");

        if (!latitude.HasValue)
            AddError(errors, "latitude", "Latitude is required.");
        else if (latitude.Value is < -90m or > 90m)
            AddError(errors, "latitude", "Latitude must be between -90 and 90.");

        if (!longitude.HasValue)
            AddError(errors, "longitude", "Longitude is required.");
        else if (longitude.Value is < -180m or > 180m)
            AddError(errors, "longitude", "Longitude must be between -180 and 180.");
    }

    private async Task<User?> ValidateOperatorAsync(Guid operatorId, Dictionary<string, List<string>> errors)
    {
        var op = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == operatorId);
        if (op is null || op.Status != "ACTIVE" || op.Role.Code != AppRoles.StationOperator)
        {
            AddError(errors, "operatorId", "Operator must be an existing active user with the STATION_OPERATOR role.");
            return null;
        }

        return op;
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
