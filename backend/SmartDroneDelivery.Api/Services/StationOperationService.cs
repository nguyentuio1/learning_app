using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public class PickupVerificationResult
{
    public bool Success { get; set; }
    public ConfirmPickupResponseDto? Response { get; set; }
    public int StatusCode { get; set; } = 200;
    public string Detail { get; set; } = string.Empty;
    public int? AttemptsRemaining { get; set; }
    public DateTime? LockoutExpiresAt { get; set; }
    public int? RetryAfterSeconds { get; set; }
}

public static class OtpLockoutManager
{
    private const string ActionFailed = "PICKUP_OTP_FAILED";
    private const string ActionReset = "PICKUP_OTP_RESET";

    public static async Task<(bool IsLocked, int AttemptsRemaining, DateTime? LockoutExpiresAt, int? RetryAfterSeconds)> CheckLockAsync(
        AppDbContext db, Guid orderId, DateTime now)
    {
        var entityId = orderId.ToString();

        // 1. Find latest reset event
        var latestReset = await db.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == "DeliveryOrder" && a.EntityId == entityId && a.Action == ActionReset)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync();

        var baseId = latestReset?.Id ?? 0;

        // 2. Count failed attempts since baseId
        var failedLogs = await db.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == "DeliveryOrder" && a.EntityId == entityId && a.Action == ActionFailed && a.Id > baseId)
            .OrderByDescending(a => a.Id)
            .ToListAsync();

        if (failedLogs.Count >= 3)
        {
            var lockTime = failedLogs[0].Timestamp;
            var lockoutExpiresAt = lockTime.AddMinutes(10);
            if (lockoutExpiresAt > now)
            {
                var retryAfter = (int)Math.Ceiling((lockoutExpiresAt - now).TotalSeconds);
                return (true, 0, lockoutExpiresAt, retryAfter);
            }

            // Lockout has expired: reset!
            db.AuditLogs.Add(new AuditLog
            {
                Action = ActionReset,
                EntityName = "DeliveryOrder",
                EntityId = entityId,
                Timestamp = now
            });
            await db.SaveChangesAsync();
            return (false, 3, null, null);
        }

        return (false, 3 - failedLogs.Count, null, null);
    }

    public static async Task<(bool IsLocked, int AttemptsRemaining, DateTime? LockoutExpiresAt, int? RetryAfterSeconds)> RecordAttemptAsync(
        AppDbContext db, Guid orderId, bool isCorrect, DateTime now, Guid? userId = null)
    {
        var entityId = orderId.ToString();

        if (isCorrect)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Action = ActionReset,
                EntityName = "DeliveryOrder",
                EntityId = entityId,
                Timestamp = now
            });
            await db.SaveChangesAsync();
            return (false, 3, null, null);
        }

        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = ActionFailed,
            EntityName = "DeliveryOrder",
            EntityId = entityId,
            Timestamp = now
        });
        await db.SaveChangesAsync();

        return await CheckLockAsync(db, orderId, now);
    }

    public static async Task ResetAsync(AppDbContext db, Guid orderId, DateTime now)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Action = ActionReset,
            EntityName = "DeliveryOrder",
            EntityId = orderId.ToString(),
            Timestamp = now
        });
        await db.SaveChangesAsync();
    }
}

public interface IStationOperationService
{
    Task<StationOrderInfoDto> GetOrderInfoAsync(Guid userId, Guid stationId, string trackingNumber);
    Task<PackageIntakeResponseDto> IntakeAsync(Guid userId, Guid stationId, PackageIntakeRequest request);
    Task<RejectIntakeResponseDto> RejectIntakeAsync(Guid userId, Guid stationId, RejectIntakeRequest request);
    Task<PickupVerificationResult> PickupAsync(Guid userId, Guid stationId, ConfirmPickupRequest request);
    Task<ValidateSecureCodeResponseDto> ValidateCodeAsync(Guid userId, Guid stationId, ConfirmPickupRequest request);
}

public partial class StationOperationService : IStationOperationService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> OrderLocks = new();

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex SecureCodeRegex();

    private readonly AppDbContext _db;
    private readonly IOrderTransitionWriter _transitions;

    public StationOperationService(AppDbContext db, IOrderTransitionWriter transitions)
    {
        _db = db;
        _transitions = transitions;
    }

    public async Task<StationOrderInfoDto> GetOrderInfoAsync(Guid userId, Guid stationId, string trackingNumber)
    {
        await AuthorizeStationOperatorAsync(userId, stationId);

        var order = await _db.DeliveryOrders
            .AsNoTracking()
            .Include(o => o.Package)
            .FirstOrDefaultAsync(o => o.TrackingNumber == trackingNumber)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Order not found for this station.");

        if (order.OriginStationId != stationId && order.DestinationStationId != stationId)
            throw new ApiException(StatusCodes.Status404NotFound, "Order not found for this station.");

        var role = order.OriginStationId == stationId ? "ORIGIN" : "DESTINATION";

        return new StationOrderInfoDto(
            order.Id,
            order.TrackingNumber,
            order.Status,
            role,
            order.Package.PackageName,
            order.Package.WeightGram,
            order.Package.LengthCm,
            order.Package.WidthCm,
            order.Package.HeightCm,
            order.Package.IsFragile,
            order.OriginStationId,
            order.DestinationStationId);
    }

    public async Task<PackageIntakeResponseDto> IntakeAsync(Guid userId, Guid stationId, PackageIntakeRequest request)
    {
        using var operation = await _transitions.EnterOperationAsync();
        await AuthorizeStationOperatorAsync(userId, stationId);

        var errors = new Dictionary<string, List<string>>();
        var tracking = request.TrackingNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(tracking))
            AddError(errors, "trackingNumber", "Tracking number is required.");

        if (!request.SlotId.HasValue)
            AddError(errors, "slotId", "Slot ID is required.");

        if (!request.MeasuredWeightGram.HasValue || request.MeasuredWeightGram.Value <= 0 || request.MeasuredWeightGram.Value > 3000)
            AddError(errors, "measuredWeightGram", "Measured weight must be greater than 0 and at most 3000 grams.");

        if (!request.MeasuredLengthCm.HasValue || request.MeasuredLengthCm.Value <= 0 || request.MeasuredLengthCm.Value > 30)
            AddError(errors, "measuredLengthCm", "Measured length must be greater than 0 and at most 30 cm.");

        if (!request.MeasuredWidthCm.HasValue || request.MeasuredWidthCm.Value <= 0 || request.MeasuredWidthCm.Value > 20)
            AddError(errors, "measuredWidthCm", "Measured width must be greater than 0 and at most 20 cm.");

        if (!request.MeasuredHeightCm.HasValue || request.MeasuredHeightCm.Value <= 0 || request.MeasuredHeightCm.Value > 15)
            AddError(errors, "measuredHeightCm", "Measured height must be greater than 0 and at most 15 cm.");

        if (request.VisualInspectionPassed != true)
            AddError(errors, "visualInspectionPassed", "Visual inspection must pass.");

        ThrowIfInvalid(errors);

        await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
        var order = _db.Database.IsNpgsql()
            ? (await _db.DeliveryOrders.FromSqlInterpolated($"SELECT * FROM delivery_orders WHERE \"TrackingNumber\" = {tracking} FOR UPDATE").ToListAsync()).SingleOrDefault()
            : await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.TrackingNumber == tracking);
        if (order is null || order.OriginStationId != stationId)
            throw new ApiException(StatusCodes.Status404NotFound, "Order not found at this origin station.");
        if (order.Status != "APPROVED" || order.ScheduledDepartureTime is null || order.EstimatedDeliveryTime is null)
            throw new ApiException(StatusCodes.Status409Conflict, "Intake requires an APPROVED, scheduled order with a PLANNED assignment.");

        // Slot configuration takes station FOR UPDATE. SHARE before loading the slot
        // prevents a deletion that prechecked EMPTY from deleting an occupied intake.
        if (_db.Database.IsNpgsql())
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM landing_stations WHERE \"Id\" IN ({order.OriginStationId}, {order.DestinationStationId}) ORDER BY \"Id\" FOR SHARE");
        var mission = _db.Database.IsNpgsql()
            ? (await _db.FlightMissions.FromSqlInterpolated($"SELECT * FROM flight_missions WHERE \"OrderId\" = {order.Id} AND \"FlightStatus\" = 'PLANNED' FOR UPDATE").ToListAsync()).SingleOrDefault()
            : await _db.FlightMissions.SingleOrDefaultAsync(m => m.OrderId == order.Id && m.FlightStatus == "PLANNED");
        if (mission is null || mission.StartSlotId.HasValue || !mission.DestinationSlotId.HasValue)
            throw new ApiException(StatusCodes.Status409Conflict, "A valid PLANNED assignment is required before intake.");
        if (_db.Database.IsNpgsql())
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM drones WHERE \"Id\" = {mission.DroneId} FOR UPDATE");
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM station_slots WHERE \"Id\" IN ({request.SlotId!.Value}, {mission.DestinationSlotId.Value}) ORDER BY \"Id\" FOR UPDATE");
        }
        var drone = await _db.Drones.SingleOrDefaultAsync(d => d.Id == mission.DroneId);
        var destination = await _db.StationSlots.SingleOrDefaultAsync(s => s.Id == mission.DestinationSlotId.Value);
        if (drone is null || drone.Status != "ASSIGNED" || drone.CurrentStationId != stationId
            || destination is null || destination.StationId != order.DestinationStationId || destination.Status != "RESERVED")
            throw new ApiException(StatusCodes.Status409Conflict, "The planned assignment resources are no longer valid.");
        var slot = await _db.StationSlots.SingleOrDefaultAsync(s => s.Id == request.SlotId!.Value && s.StationId == stationId)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Slot not found at this origin station.");
        if (slot.Status != "EMPTY")
            throw new ApiException(StatusCodes.Status409Conflict, $"Slot status is {slot.Status}, must be EMPTY.");
        var now = DateTime.UtcNow;
        await _transitions.StageAsync(order, "APPROVED", "READY_FOR_TAKEOFF", userId,
            $"Package intake completed, loaded into slot {slot.SlotNumber}", now);
        mission.StartSlotId = slot.Id;
        slot.Status = "OCCUPIED";
        slot.LastUpdated = now;
        _db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "PACKAGE_INTAKE_CONFIRMATION",
            EntityName = "DeliveryOrder", EntityId = order.Id.ToString(), Timestamp = now });
        _db.Notifications.Add(new Notification { UserId = order.CustomerId, OrderId = order.Id, Type = "ORDER_STATUS",
            Title = "Ready for takeoff", Message = "Your parcel has been verified and loaded at origin station.", CreatedAt = now });
        await _db.SaveChangesAsync();
        if (tx is not null) await tx.CommitAsync();
        return new(order.Id, order.TrackingNumber, order.Status, slot.Id, slot.SlotNumber, now);
    }

    public async Task<RejectIntakeResponseDto> RejectIntakeAsync(Guid userId, Guid stationId, RejectIntakeRequest request)
    {
        using var operation = await _transitions.EnterOperationAsync();
        await AuthorizeStationOperatorAsync(userId, stationId);

        var errors = new Dictionary<string, List<string>>();
        var tracking = request.TrackingNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(tracking))
            AddError(errors, "trackingNumber", "Tracking number is required.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 5 or > 500)
            AddError(errors, "reason", "Reason is required and must be between 5 and 500 characters.");

        ThrowIfInvalid(errors);

        var sem = OrderLocks.GetOrAdd(tracking, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            // PostgreSQL: lock the order row before reading it, so a concurrent intake or reject on
            // another instance cannot pass the APPROVED check at the same time.
            await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
            if (tx != null)
            {
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT \"Id\" FROM delivery_orders WHERE \"TrackingNumber\" = {tracking} FOR UPDATE");
            }

            var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.TrackingNumber == tracking)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "Order not found at this origin station.");

            if (order.OriginStationId != stationId)
                throw new ApiException(StatusCodes.Status404NotFound, "Order origin is not this station.");

            if (order.Status != "APPROVED")
                throw new ApiException(StatusCodes.Status409Conflict, $"Order status is {order.Status}, must be APPROVED.");

            var now = DateTime.UtcNow;
            await _transitions.StageAsync(order, "APPROVED", "FAILED", userId, reason, now);
            order.CancelReason = reason;

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "PACKAGE_INTAKE_REJECT",
                EntityName = "DeliveryOrder",
                EntityId = order.Id.ToString(),
                Timestamp = now
            });

            _db.Notifications.Add(new Notification
            {
                UserId = order.CustomerId,
                OrderId = order.Id,
                Type = "ORDER_STATUS",
                Title = "Package Intake Failed",
                Message = reason,
                CreatedAt = now
            });

            await _db.SaveChangesAsync();
            if (tx != null)
                await tx.CommitAsync();

            return new RejectIntakeResponseDto(order.Id, order.TrackingNumber, order.Status);
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<PickupVerificationResult> PickupAsync(Guid userId, Guid stationId, ConfirmPickupRequest request)
    {
        using var operation = await _transitions.EnterOperationAsync();
        await AuthorizeStationOperatorAsync(userId, stationId);

        var errors = new Dictionary<string, List<string>>();
        var tracking = request.TrackingNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(tracking))
            AddError(errors, "trackingNumber", "Tracking number is required.");

        var code = request.PickupSecureCode?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(code) || !SecureCodeRegex().IsMatch(code))
            AddError(errors, "pickupSecureCode", "Pickup secure code must be exactly 6 digits.");

        ThrowIfInvalid(errors);

        if (_db.Database.IsRelational())
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            // Exclusive row-level lock on order in PostgreSQL before reading order or OTP state
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM delivery_orders WHERE \"TrackingNumber\" = {tracking} FOR UPDATE");

            var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.TrackingNumber == tracking)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "Order not found at this destination station.");

            if (order.DestinationStationId != stationId)
                throw new ApiException(StatusCodes.Status404NotFound, "Order destination is not this station.");

            if (order.Status != "ARRIVED")
                throw new ApiException(StatusCodes.Status409Conflict, $"Order status is {order.Status}, must be ARRIVED.");

            var now = DateTime.UtcNow;
            var lockCheck = await OtpLockoutManager.CheckLockAsync(_db, order.Id, now);
            if (lockCheck.IsLocked)
            {
                await tx.CommitAsync();
                return new PickupVerificationResult
                {
                    Success = false,
                    StatusCode = StatusCodes.Status423Locked,
                    Detail = "Too many wrong codes. Try again later.",
                    LockoutExpiresAt = lockCheck.LockoutExpiresAt,
                    RetryAfterSeconds = lockCheck.RetryAfterSeconds
                };
            }

            var isCodeMatch = order.PickupSecureCode == code;
            if (!isCodeMatch)
            {
                var attemptResult = await OtpLockoutManager.RecordAttemptAsync(_db, order.Id, false, now, userId);
                await tx.CommitAsync();

                if (attemptResult.IsLocked)
                {
                    return new PickupVerificationResult
                    {
                        Success = false,
                        StatusCode = StatusCodes.Status423Locked,
                        Detail = "Too many wrong codes. Try again later.",
                        LockoutExpiresAt = attemptResult.LockoutExpiresAt,
                        RetryAfterSeconds = attemptResult.RetryAfterSeconds
                    };
                }

                return new PickupVerificationResult
                {
                    Success = false,
                    StatusCode = StatusCodes.Status400BadRequest,
                    Detail = "Invalid pickup code.",
                    AttemptsRemaining = attemptResult.AttemptsRemaining
                };
            }

            // Contract 7.3: Destination slot must be explicitly linked from FlightMission
            var mission = await _db.FlightMissions
                .Include(m => m.DestinationSlot)
                .Where(m => m.OrderId == order.Id && m.FlightStatus == "COMPLETED")
                .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).FirstOrDefaultAsync();

            if (mission?.DestinationSlot == null || mission.DestinationSlot.StationId != stationId)
            {
                throw new ApiException(StatusCodes.Status409Conflict,
                    "Order has no destination slot linked to an active or completed flight mission.");
            }

            var slot = mission.DestinationSlot;
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM station_slots WHERE \"Id\" = {slot.Id} FOR UPDATE");

            if (slot.Status != "OCCUPIED" || !await MissionResources.IsLatestSlotClaimAsync(_db, mission!, slot.Id))
                throw new ApiException(StatusCodes.Status409Conflict, "The destination slot no longer holds this parcel.");
            await OtpLockoutManager.RecordAttemptAsync(_db, order.Id, true, now, userId);

            await _transitions.StageAsync(order, "ARRIVED", "DELIVERED", userId,
                "Secure code verified, parcel handed over to recipient", now);
            order.ActualDeliveryTime = now;
            slot.Status = "EMPTY";
            slot.LastUpdated = now;

            // BR-LS-22: NEVER store pickupSecureCode in logs or audit entries!
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "PACKAGE_DELIVERY_CONFIRMATION",
                EntityName = "DeliveryOrder",
                EntityId = order.Id.ToString(),
                Timestamp = now
            });

            _db.Notifications.Add(new Notification
            {
                UserId = order.CustomerId,
                OrderId = order.Id,
                Type = "ORDER_STATUS",
                Title = "Package Delivered",
                Message = "Your delivery has been successfully picked up.",
                CreatedAt = now
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return new PickupVerificationResult
            {
                Success = true,
                Response = new ConfirmPickupResponseDto(
                    order.Id,
                    order.TrackingNumber,
                    order.Status,
                    now,
                    slot.Id,
                    true)
            };
        }

        var sem = OrderLocks.GetOrAdd(tracking, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;

            var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.TrackingNumber == tracking)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "Order not found at this destination station.");

            if (order.DestinationStationId != stationId)
                throw new ApiException(StatusCodes.Status404NotFound, "Order destination is not this station.");

            if (order.Status != "ARRIVED")
                throw new ApiException(StatusCodes.Status409Conflict, $"Order status is {order.Status}, must be ARRIVED.");

            var lockCheck = await OtpLockoutManager.CheckLockAsync(_db, order.Id, now);
            if (lockCheck.IsLocked)
            {
                return new PickupVerificationResult
                {
                    Success = false,
                    StatusCode = StatusCodes.Status423Locked,
                    Detail = "Too many wrong codes. Try again later.",
                    LockoutExpiresAt = lockCheck.LockoutExpiresAt,
                    RetryAfterSeconds = lockCheck.RetryAfterSeconds
                };
            }

            var isCodeMatch = order.PickupSecureCode == code;
            if (!isCodeMatch)
            {
                var attemptResult = await OtpLockoutManager.RecordAttemptAsync(_db, order.Id, false, now, userId);
                if (attemptResult.IsLocked)
                {
                    return new PickupVerificationResult
                    {
                        Success = false,
                        StatusCode = StatusCodes.Status423Locked,
                        Detail = "Too many wrong codes. Try again later.",
                        LockoutExpiresAt = attemptResult.LockoutExpiresAt,
                        RetryAfterSeconds = attemptResult.RetryAfterSeconds
                    };
                }

                return new PickupVerificationResult
                {
                    Success = false,
                    StatusCode = StatusCodes.Status400BadRequest,
                    Detail = "Invalid pickup code.",
                    AttemptsRemaining = attemptResult.AttemptsRemaining
                };
            }

            // Contract 7.3: Destination slot must be explicitly linked from FlightMission
            var mission = await _db.FlightMissions
                .Include(m => m.DestinationSlot)
                .Where(m => m.OrderId == order.Id && m.FlightStatus == "COMPLETED")
                .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).FirstOrDefaultAsync();

            if (mission?.DestinationSlot == null || mission.DestinationSlot.StationId != stationId)
            {
                throw new ApiException(StatusCodes.Status409Conflict,
                    "Order has no destination slot linked to an active or completed flight mission.");
            }

            var slot = mission.DestinationSlot;
            if (slot.Status != "OCCUPIED" || !await MissionResources.IsLatestSlotClaimAsync(_db, mission, slot.Id))
                throw new ApiException(StatusCodes.Status409Conflict, "The destination slot no longer holds this parcel.");
            await OtpLockoutManager.RecordAttemptAsync(_db, order.Id, true, now, userId);

            await _transitions.StageAsync(order, "ARRIVED", "DELIVERED", userId,
                "Secure code verified, parcel handed over to recipient", now);
            order.ActualDeliveryTime = now;
            slot.Status = "EMPTY";
            slot.LastUpdated = now;

            // BR-LS-22: NEVER store pickupSecureCode in logs or audit entries!
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "PACKAGE_DELIVERY_CONFIRMATION",
                EntityName = "DeliveryOrder",
                EntityId = order.Id.ToString(),
                Timestamp = now
            });

            _db.Notifications.Add(new Notification
            {
                UserId = order.CustomerId,
                OrderId = order.Id,
                Type = "ORDER_STATUS",
                Title = "Package Delivered",
                Message = "Your delivery has been successfully picked up.",
                CreatedAt = now
            });

            await _db.SaveChangesAsync();

            return new PickupVerificationResult
            {
                Success = true,
                Response = new ConfirmPickupResponseDto(
                    order.Id,
                    order.TrackingNumber,
                    order.Status,
                    now,
                    slot.Id,
                    true)
            };
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<ValidateSecureCodeResponseDto> ValidateCodeAsync(Guid userId, Guid stationId, ConfirmPickupRequest request)
    {
        await AuthorizeStationOperatorAsync(userId, stationId);

        var errors = new Dictionary<string, List<string>>();
        var tracking = request.TrackingNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(tracking))
            AddError(errors, "trackingNumber", "Tracking number is required.");

        var code = request.PickupSecureCode?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(code) || !SecureCodeRegex().IsMatch(code))
            AddError(errors, "pickupSecureCode", "Pickup secure code must be exactly 6 digits.");

        ThrowIfInvalid(errors);

        if (_db.Database.IsRelational())
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            // Exclusive row-level lock on order in PostgreSQL before reading order or OTP state
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM delivery_orders WHERE \"TrackingNumber\" = {tracking} FOR UPDATE");

            var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.TrackingNumber == tracking)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "Order not found at this destination station.");

            if (order.DestinationStationId != stationId)
                throw new ApiException(StatusCodes.Status404NotFound, "Order destination is not this station.");

            if (order.Status != "ARRIVED")
                throw new ApiException(StatusCodes.Status409Conflict, $"Order status is {order.Status}, must be ARRIVED.");

            var now = DateTime.UtcNow;
            var lockCheck = await OtpLockoutManager.CheckLockAsync(_db, order.Id, now);
            if (lockCheck.IsLocked)
            {
                await tx.CommitAsync();
                return new ValidateSecureCodeResponseDto(
                    false,
                    0,
                    true,
                    lockCheck.LockoutExpiresAt);
            }

            var isCodeMatch = order.PickupSecureCode == code;
            var attemptResult = await OtpLockoutManager.RecordAttemptAsync(_db, order.Id, isCodeMatch, now, userId);
            await tx.CommitAsync();

            return new ValidateSecureCodeResponseDto(
                isCodeMatch,
                attemptResult.AttemptsRemaining,
                attemptResult.IsLocked,
                attemptResult.LockoutExpiresAt);
        }

        var sem = OrderLocks.GetOrAdd(tracking, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.TrackingNumber == tracking)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "Order not found at this destination station.");

            if (order.DestinationStationId != stationId)
                throw new ApiException(StatusCodes.Status404NotFound, "Order destination is not this station.");

            if (order.Status != "ARRIVED")
                throw new ApiException(StatusCodes.Status409Conflict, $"Order status is {order.Status}, must be ARRIVED.");

            var now = DateTime.UtcNow;
            var lockCheck = await OtpLockoutManager.CheckLockAsync(_db, order.Id, now);
            if (lockCheck.IsLocked)
            {
                return new ValidateSecureCodeResponseDto(
                    false,
                    0,
                    true,
                    lockCheck.LockoutExpiresAt);
            }

            var isCodeMatch = order.PickupSecureCode == code;
            var attemptResult = await OtpLockoutManager.RecordAttemptAsync(_db, order.Id, isCodeMatch, now, userId);

            return new ValidateSecureCodeResponseDto(
                isCodeMatch,
                attemptResult.AttemptsRemaining,
                attemptResult.IsLocked,
                attemptResult.LockoutExpiresAt);
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task AuthorizeStationOperatorAsync(Guid userId, Guid stationId)
    {
        var caller = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");

        if (caller.Status != "ACTIVE")
            throw new ApiException(StatusCodes.Status403Forbidden, "This account is not active.");

        var station = await _db.LandingStations.AsNoTracking().FirstOrDefaultAsync(s => s.Id == stationId)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Landing station not found.");

        if (caller.Role.Code != AppRoles.Admin)
        {
            if (caller.Role.Code != AppRoles.StationOperator || station.OperatorId != userId)
                throw new ApiException(StatusCodes.Status403Forbidden, "You do not have permission to operate at this station.");
        }
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
