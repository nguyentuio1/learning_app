using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

public interface IDroneService
{
    Task<IReadOnlyList<DroneDto>> ListAsync(Guid userId, string? status, Guid? stationId, string? search);
    Task<DroneDetailDto> GetByIdAsync(Guid userId, Guid id);
    Task<DroneDto> CreateAsync(Guid userId, CreateDroneRequest request);
    Task<DroneDto> UpdateAsync(Guid userId, Guid id, UpdateDroneRequest request);
    Task<DroneDto> MaintenanceAsync(Guid userId, Guid id, DroneMaintenanceRequest request);
}

public class DroneService : IDroneService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> DroneLocks = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SerialLocks = new(StringComparer.OrdinalIgnoreCase);
    public const string SerialNumberUniqueConstraint = "IX_drones_SerialNumber";

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.Ordinal)
    {
        "IDLE", "ASSIGNED", "FLYING", "CHARGING", "MAINTENANCE"
    };

    private readonly AppDbContext _db;
    private readonly IOrderTransitionWriter _transitions;

    public DroneService(AppDbContext db, IOrderTransitionWriter? transitions = null)
    {
        _db = db;
        _transitions = transitions ?? new OrderTransitionWriter(db);
    }

    public async Task<IReadOnlyList<DroneDto>> ListAsync(Guid userId, string? status, Guid? stationId, string? search)
    {
        await LoadActiveUserAsync(userId);

        var query = _db.Drones
            .AsNoTracking()
            .Include(d => d.CurrentStation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = status.Trim().ToUpperInvariant();
            if (!ValidStatuses.Contains(wanted))
            {
                throw new ApiException(StatusCodes.Status400BadRequest, "One or more validation errors occurred.",
                    new Dictionary<string, string[]>
                    {
                        ["status"] = ["Status must be one of: IDLE, ASSIGNED, FLYING, CHARGING, MAINTENANCE."]
                    });
            }

            query = query.Where(d => d.Status == wanted);
        }

        if (stationId.HasValue)
        {
            query = query.Where(d => d.CurrentStationId == stationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(d =>
                d.SerialNumber.ToLower().Contains(term) ||
                d.ModelName.ToLower().Contains(term));
        }

        var drones = await query.OrderBy(d => d.SerialNumber).ToListAsync();
        return drones.Select(DroneDto.From).ToList();
    }

    public async Task<DroneDetailDto> GetByIdAsync(Guid userId, Guid id)
    {
        await LoadActiveUserAsync(userId);

        var drone = await _db.Drones
            .AsNoTracking()
            .Include(d => d.CurrentStation)
            .FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Drone not found.");

        var activeMission = await _db.FlightMissions
            .AsNoTracking()
            .Where(m => m.DroneId == id && (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR"))
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync();

        var dto = DroneDto.From(drone);
        return new DroneDetailDto(
            dto.Id,
            dto.SerialNumber,
            dto.ModelName,
            dto.MaxPayloadGram,
            dto.MaxFlightRangeKm,
            dto.BatteryCapacityMah,
            dto.Status,
            dto.CurrentStationId,
            dto.CurrentStationName,
            dto.CreatedAt,
            activeMission);
    }

    public async Task<DroneDto> CreateAsync(Guid userId, CreateDroneRequest request)
    {
        await LoadActiveUserAsync(userId);

        var errors = new Dictionary<string, List<string>>();
        var serial = request.SerialNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(serial))
            AddError(errors, "serialNumber", "Serial number is required.");
        else if (serial.Length > 100)
            AddError(errors, "serialNumber", "Serial number must be at most 100 characters.");

        ValidateSpecs(request.ModelName, request.MaxPayloadGram, request.MaxFlightRangeKm, request.BatteryCapacityMah, errors);

        LandingStation? station = null;
        if (request.CurrentStationId.HasValue)
        {
            station = await _db.LandingStations.FirstOrDefaultAsync(s => s.Id == request.CurrentStationId.Value);
            if (station is null)
                AddError(errors, "currentStationId", "Current station does not exist.");
        }

        ThrowIfInvalid(errors);

        if (await _db.Drones.AnyAsync(d => d.SerialNumber == serial))
            throw new ApiException(StatusCodes.Status409Conflict, "Serial number is already used.");

        var now = DateTime.UtcNow;
        var drone = new Drone
        {
            Id = Guid.NewGuid(),
            SerialNumber = serial,
            ModelName = request.ModelName!.Trim(),
            MaxPayloadGram = request.MaxPayloadGram!.Value,
            MaxFlightRangeKm = request.MaxFlightRangeKm!.Value,
            BatteryCapacityMah = request.BatteryCapacityMah!.Value,
            Status = "MAINTENANCE",
            CurrentStationId = station?.Id,
            CurrentStation = station,
            CreatedAt = now
        };

        var audit = new AuditLog
        {
            UserId = userId,
            Action = "DRONE_CREATE",
            EntityName = "Drone",
            EntityId = drone.Id.ToString(),
            Timestamp = now
        };

        if (_db.Database.IsRelational())
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                _db.Drones.Add(drone);
                _db.AuditLogs.Add(audit);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync();
                if (IsDuplicateSerialException(ex))
                {
                    throw new ApiException(StatusCodes.Status409Conflict, "Serial number is already used.");
                }
                throw;
            }
        }
        else
        {
            var sem = SerialLocks.GetOrAdd(serial, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync();
            try
            {
                if (await _db.Drones.AnyAsync(d => d.SerialNumber == serial))
                    throw new ApiException(StatusCodes.Status409Conflict, "Serial number is already used.");

                _db.Drones.Add(drone);
                _db.AuditLogs.Add(audit);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                if (IsDuplicateSerialException(ex))
                {
                    throw new ApiException(StatusCodes.Status409Conflict, "Serial number is already used.");
                }
                throw;
            }
            finally
            {
                sem.Release();
            }
        }

        return DroneDto.From(drone);
    }

    public async Task<DroneDto> UpdateAsync(Guid userId, Guid id, UpdateDroneRequest request)
    {
        using var operation = await _transitions.EnterOperationAsync();
        await LoadActiveUserAsync(userId);
        var errors = new Dictionary<string, List<string>>();
        ValidateSpecs(request.ModelName, request.MaxPayloadGram, request.MaxFlightRangeKm, request.BatteryCapacityMah, errors);
        ThrowIfInvalid(errors);
        await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
        // Assignment also locks the drone before checking eligibility. Never lock an
        // order after this row: that would reverse assignment's order -> drone locks.
        var drone = _db.Database.IsNpgsql()
            ? (await _db.Drones.FromSqlInterpolated($"SELECT * FROM drones WHERE \"Id\" = {id} FOR UPDATE").ToListAsync()).SingleOrDefault()
            : await _db.Drones.FirstOrDefaultAsync(d => d.Id == id);
        if (drone is null) throw new ApiException(StatusCodes.Status404NotFound, "Drone not found.");
        await _db.Entry(drone).ReloadAsync();
        await _db.Entry(drone).Reference(d => d.CurrentStation).LoadAsync();
        var hasActiveMission = await _db.FlightMissions.AsNoTracking()
            .AnyAsync(m => m.DroneId == id && (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR"));
        if (hasActiveMission || drone.Status is "ASSIGNED" or "FLYING")
            throw new ApiException(StatusCodes.Status409Conflict, "The drone has an active mission. Resolve it in the dispatch workflow first.");
        drone.ModelName = request.ModelName!.Trim();
        drone.MaxPayloadGram = request.MaxPayloadGram!.Value;
        drone.MaxFlightRangeKm = request.MaxFlightRangeKm!.Value;
        drone.BatteryCapacityMah = request.BatteryCapacityMah!.Value;
        _db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "DRONE_UPDATE", EntityName = "Drone", EntityId = drone.Id.ToString(), Timestamp = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        if (tx is not null) await tx.CommitAsync();
        return DroneDto.From(drone);
    }

    public async Task<DroneDto> MaintenanceAsync(Guid userId, Guid id, DroneMaintenanceRequest request)
    {
        await LoadActiveUserAsync(userId);

        var sem = DroneLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            var drone = await _db.Drones
                .Include(d => d.CurrentStation)
                .FirstOrDefaultAsync(d => d.Id == id)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "Drone not found.");

        var errors = new Dictionary<string, List<string>>();
        var action = request.Action?.Trim().ToUpperInvariant() ?? string.Empty;
        if (action is not ("START" or "COMPLETE"))
            AddError(errors, "action", "Action must be START or COMPLETE.");

        var expectedStatus = request.ExpectedStatus?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!ValidStatuses.Contains(expectedStatus))
            AddError(errors, "expectedStatus", "Expected status must be a valid drone status.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 5 or > 500)
            AddError(errors, "reason", "Reason is required and must be between 5 and 500 characters.");

        if (action == "COMPLETE")
        {
            if (request.InspectionPassed != true)
                AddError(errors, "inspectionPassed", "Inspection passed must be true for maintenance completion.");
            if (!request.ReadyForService.HasValue)
                AddError(errors, "readyForService", "Ready for service flag is required for maintenance completion.");
        }

        ThrowIfInvalid(errors);

        var hasActiveMission = await _db.FlightMissions
            .AsNoTracking()
            .AnyAsync(m => m.DroneId == id && (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR"));
        if (hasActiveMission || drone.Status is "ASSIGNED" or "FLYING")
        {
            throw new ApiException(StatusCodes.Status409Conflict,
                "The drone has an active mission. Resolve it in the dispatch workflow first.");
        }

        if (drone.Status != expectedStatus)
        {
            throw new ApiException(StatusCodes.Status409Conflict,
                $"The drone status changed to {drone.Status}. Refresh and try again.");
        }

        string targetStatus;
        string auditAction;
        if (action == "START")
        {
            if (drone.Status is not ("IDLE" or "CHARGING"))
            {
                throw new ApiException(StatusCodes.Status409Conflict,
                    $"Cannot start maintenance from status {drone.Status}. Must be IDLE or CHARGING.");
            }
            targetStatus = "MAINTENANCE";
            auditAction = "DRONE_MAINTENANCE_START";
        }
        else // COMPLETE
        {
            if (drone.Status != "MAINTENANCE")
            {
                throw new ApiException(StatusCodes.Status409Conflict,
                    $"Cannot complete maintenance when drone status is {drone.Status}. Must be MAINTENANCE.");
            }
            targetStatus = request.ReadyForService!.Value ? "IDLE" : "CHARGING";
            auditAction = "DRONE_MAINTENANCE_COMPLETE";
        }

        var now = DateTime.UtcNow;
        var audit = new AuditLog
        {
            UserId = userId,
            Action = auditAction,
            EntityName = "Drone",
            EntityId = drone.Id.ToString(),
            Timestamp = now
        };

        await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;

        if (_db.Database.IsRelational())
        {
            var updatedRows = await _db.Drones
                .Where(d => d.Id == id && d.Status == expectedStatus)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, targetStatus));

            if (updatedRows == 0)
            {
                throw new ApiException(StatusCodes.Status409Conflict,
                    "The drone status was updated concurrently. Refresh and try again.");
            }

            _db.Entry(drone).State = EntityState.Unchanged;
            _db.AuditLogs.Add(audit);
            await _db.SaveChangesAsync();
            await tx!.CommitAsync();
            drone.Status = targetStatus;
        }
        else
        {
            if (drone.Status != expectedStatus)
            {
                throw new ApiException(StatusCodes.Status409Conflict,
                    "The drone status was updated concurrently. Refresh and try again.");
            }
            drone.Status = targetStatus;
            _db.AuditLogs.Add(audit);
            await _db.SaveChangesAsync();
        }

        return DroneDto.From(drone);
        }
        finally
        {
            sem.Release();
        }
    }

    public static bool IsDuplicateSerialException(DbUpdateException ex)
    {
        return ex.InnerException is Npgsql.PostgresException
        {
            SqlState: Npgsql.PostgresErrorCodes.UniqueViolation,
            ConstraintName: SerialNumberUniqueConstraint
        };
    }

    private static void ValidateSpecs(
        string? modelName, decimal? maxPayloadGram, decimal? maxFlightRangeKm, int? batteryCapacityMah,
        Dictionary<string, List<string>> errors)
    {
        var model = modelName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(model))
            AddError(errors, "modelName", "Model name is required.");
        else if (model.Length > 100)
            AddError(errors, "modelName", "Model name must be at most 100 characters.");

        if (!maxPayloadGram.HasValue)
            AddError(errors, "maxPayloadGram", "Max payload gram is required.");
        else if (maxPayloadGram.Value is <= 0 or > 3000)
            AddError(errors, "maxPayloadGram", "Max payload must be greater than 0 and at most 3000 grams.");

        if (!maxFlightRangeKm.HasValue)
            AddError(errors, "maxFlightRangeKm", "Max flight range km is required.");
        else if (maxFlightRangeKm.Value is <= 0 or > 9999.99m)
            AddError(errors, "maxFlightRangeKm", "Max flight range must be greater than 0 and at most 9999.99 km.");

        if (!batteryCapacityMah.HasValue)
            AddError(errors, "batteryCapacityMah", "Battery capacity is required.");
        else if (batteryCapacityMah.Value <= 0)
            AddError(errors, "batteryCapacityMah", "Battery capacity must be greater than 0.");
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
