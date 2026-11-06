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
    private static readonly ConcurrentDictionary<string, (int Attempts, DateTime? LockedUntil)> Lockouts = new();

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex SecureCodeRegex();

    private readonly AppDbContext _db;

    public StationOperationService(AppDbContext db)
    {
        _db = db;
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

        var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.TrackingNumber == tracking)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Order not found at this origin station.");

        if (order.OriginStationId != stationId)
            throw new ApiException(StatusCodes.Status404NotFound, "Order origin is not this station.");

        if (order.Status != "APPROVED")
            throw new ApiException(StatusCodes.Status409Conflict, $"Order status is {order.Status}, must be APPROVED.");

        var slot = await _db.StationSlots.FirstOrDefaultAsync(s => s.Id == request.SlotId!.Value && s.StationId == stationId)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Slot not found at this origin station.");

        if (slot.Status != "EMPTY")
            throw new ApiException(StatusCodes.Status409Conflict, $"Slot status is {slot.Status}, must be EMPTY.");

        var now = DateTime.UtcNow;
        order.Status = "READY_FOR_TAKEOFF";
        order.UpdatedAt = now;
        slot.Status = "OCCUPIED";
        slot.LastUpdated = now;

        _db.DeliveryStatusLogs.Add(new DeliveryStatusLog
        {
            OrderId = order.Id,
            FromStatus = "APPROVED",
            ToStatus = "READY_FOR_TAKEOFF",
            ChangedBy = userId,
            Remarks = $"Package intake completed, loaded into slot {slot.SlotNumber}",
            CreatedAt = now
        });

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "PACKAGE_INTAKE_CONFIRMATION",
            EntityName = "DeliveryOrder",
            EntityId = order.Id.ToString(),
            Timestamp = now
        });

        _db.Notifications.Add(new Notification
        {
            UserId = order.CustomerId,
            OrderId = order.Id,
            Type = "ORDER_STATUS",
            Title = "Ready for takeoff",
            Message = "Your parcel has been verified and loaded at origin station.",
            CreatedAt = now
        });

        if (_db.Database.IsRelational())
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        else
        {
            await _db.SaveChangesAsync();
        }

        return new PackageIntakeResponseDto(
            order.Id,
            order.TrackingNumber,
            order.Status,
            slot.Id,
            slot.SlotNumber,
            now);
    }

    public async Task<RejectIntakeResponseDto> RejectIntakeAsync(Guid userId, Guid stationId, RejectIntakeRequest request)
    {
        await AuthorizeStationOperatorAsync(userId, stationId);

        var errors = new Dictionary<string, List<string>>();
        var tracking = request.TrackingNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(tracking))
            AddError(errors, "trackingNumber", "Tracking number is required.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 5 or > 500)
            AddError(errors, "reason", "Reason is required and must be between 5 and 500 characters.");

        ThrowIfInvalid(errors);

        var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.TrackingNumber == tracking)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Order not found at this origin station.");

        if (order.OriginStationId != stationId)
            throw new ApiException(StatusCodes.Status404NotFound, "Order origin is not this station.");

        if (order.Status != "APPROVED")
            throw new ApiException(StatusCodes.Status409Conflict, $"Order status is {order.Status}, must be APPROVED.");

        var now = DateTime.UtcNow;
        order.Status = "FAILED";
        order.CancelReason = reason;
        order.UpdatedAt = now;

        _db.DeliveryStatusLogs.Add(new DeliveryStatusLog
        {
            OrderId = order.Id,
            FromStatus = "APPROVED",
            ToStatus = "FAILED",
            ChangedBy = userId,
            Remarks = reason,
            CreatedAt = now
        });

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

        if (_db.Database.IsRelational())
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        else
        {
            await _db.SaveChangesAsync();
        }

        return new RejectIntakeResponseDto(order.Id, order.TrackingNumber, order.Status);
    }

    public async Task<PickupVerificationResult> PickupAsync(Guid userId, Guid stationId, ConfirmPickupRequest request)
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

        var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.TrackingNumber == tracking)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Order not found at this destination station.");

        if (order.DestinationStationId != stationId)
            throw new ApiException(StatusCodes.Status404NotFound, "Order destination is not this station.");

        if (order.Status != "ARRIVED")
            throw new ApiException(StatusCodes.Status409Conflict, $"Order status is {order.Status}, must be ARRIVED.");

        var now = DateTime.UtcNow;
        var lockInfo = Lockouts.GetOrAdd(tracking, _ => (0, null));

        if (lockInfo.LockedUntil.HasValue && lockInfo.LockedUntil.Value > now)
        {
            var seconds = (int)Math.Ceiling((lockInfo.LockedUntil.Value - now).TotalSeconds);
            return new PickupVerificationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status423Locked,
                Detail = "Too many wrong codes. Try again later.",
                LockoutExpiresAt = lockInfo.LockedUntil.Value,
                RetryAfterSeconds = seconds
            };
        }

        if (order.PickupSecureCode != code)
        {
            var newAttempts = lockInfo.Attempts + 1;
            if (newAttempts >= 3)
            {
                var lockExpires = now.AddMinutes(10);
                Lockouts[tracking] = (3, lockExpires);
                return new PickupVerificationResult
                {
                    Success = false,
                    StatusCode = StatusCodes.Status423Locked,
                    Detail = "Too many wrong codes. Try again later.",
                    LockoutExpiresAt = lockExpires,
                    RetryAfterSeconds = 600
                };
            }

            Lockouts[tracking] = (newAttempts, null);
            return new PickupVerificationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status400BadRequest,
                Detail = "Invalid pickup code.",
                AttemptsRemaining = 3 - newAttempts
            };
        }

        // Code correct -> reset lockout
        Lockouts.TryRemove(tracking, out _);

        var mission = await _db.FlightMissions
            .Include(m => m.DestinationSlot)
            .FirstOrDefaultAsync(m => m.OrderId == order.Id);

        var slot = mission?.DestinationSlot
            ?? await _db.StationSlots.FirstOrDefaultAsync(s => s.StationId == stationId && s.Status == "OCCUPIED")
            ?? await _db.StationSlots.FirstAsync(s => s.StationId == stationId);

        order.Status = "DELIVERED";
        order.ActualDeliveryTime = now;
        order.UpdatedAt = now;
        slot.Status = "EMPTY";
        slot.LastUpdated = now;

        _db.DeliveryStatusLogs.Add(new DeliveryStatusLog
        {
            OrderId = order.Id,
            FromStatus = "ARRIVED",
            ToStatus = "DELIVERED",
            ChangedBy = userId,
            Remarks = "Secure code verified, parcel handed over to recipient",
            CreatedAt = now
        });

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

        if (_db.Database.IsRelational())
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        else
        {
            await _db.SaveChangesAsync();
        }

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

    public async Task<ValidateSecureCodeResponseDto> ValidateCodeAsync(Guid userId, Guid stationId, ConfirmPickupRequest request)
    {
        await AuthorizeStationOperatorAsync(userId, stationId);

        var tracking = request.TrackingNumber?.Trim() ?? string.Empty;
        var code = request.PickupSecureCode?.Trim() ?? string.Empty;

        var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.TrackingNumber == tracking)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Order not found at this destination station.");

        if (order.DestinationStationId != stationId)
            throw new ApiException(StatusCodes.Status404NotFound, "Order destination is not this station.");

        if (order.Status != "ARRIVED")
            throw new ApiException(StatusCodes.Status409Conflict, $"Order status is {order.Status}, must be ARRIVED.");

        var now = DateTime.UtcNow;
        var lockInfo = Lockouts.GetOrAdd(tracking, _ => (0, null));

        if (lockInfo.LockedUntil.HasValue && lockInfo.LockedUntil.Value > now)
        {
            return new ValidateSecureCodeResponseDto(
                false,
                0,
                true,
                lockInfo.LockedUntil.Value);
        }

        if (order.PickupSecureCode != code)
        {
            var newAttempts = lockInfo.Attempts + 1;
            if (newAttempts >= 3)
            {
                var lockExpires = now.AddMinutes(10);
                Lockouts[tracking] = (3, lockExpires);
                return new ValidateSecureCodeResponseDto(
                    false,
                    0,
                    true,
                    lockExpires);
            }

            Lockouts[tracking] = (newAttempts, null);
            return new ValidateSecureCodeResponseDto(
                false,
                3 - newAttempts,
                false,
                null);
        }

        Lockouts.TryRemove(tracking, out _);
        return new ValidateSecureCodeResponseDto(
            true,
            3,
            false,
            null);
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
