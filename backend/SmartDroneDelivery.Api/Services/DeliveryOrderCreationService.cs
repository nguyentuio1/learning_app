using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

public interface IDeliveryOrderCreationService
{
    Task<CreatedDeliveryOrderDto> CreateAsync(Guid customerId, CreateDeliveryOrderRequest request);
}

public class DeliveryOrderCreationService : IDeliveryOrderCreationService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> PackageLocks = new();
    private static readonly Regex PhoneRegex = new(@"^\d{9,11}$", RegexOptions.Compiled);

    private readonly AppDbContext _db;

    public DeliveryOrderCreationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CreatedDeliveryOrderDto> CreateAsync(Guid customerId, CreateDeliveryOrderRequest request)
    {
        await EnsureActiveCustomerAsync(customerId);
        ValidateCreateRequestFields(request);

        var packageLock = PackageLocks.GetOrAdd(request.PackageId, _ => new SemaphoreSlim(1, 1));
        await packageLock.WaitAsync();
        try
        {
            var package = await _db.Packages
                .Include(p => p.DeliveryOrder)
                .FirstOrDefaultAsync(p => p.Id == request.PackageId);

            if (package == null || package.SenderId != customerId)
                throw new ApiException(StatusCodes.Status404NotFound, "Package not found.");

            if (package.DeliveryOrder != null)
                throw new ApiException(StatusCodes.Status409Conflict, "This package is already linked to a delivery order.");

            ValidatePackageLimitsForOrder(package);

            var origin = await _db.LandingStations.FindAsync(request.OriginStationId);
            if (origin == null)
                throw new ApiException(StatusCodes.Status404NotFound, "Origin landing station not found.");

            if (origin.Status != "OPERATIONAL")
                throw FieldError("originStationId", $"Origin landing station is not operational (Status: {origin.Status}).");

            var destination = await _db.LandingStations.FindAsync(request.DestinationStationId);
            if (destination == null)
                throw new ApiException(StatusCodes.Status404NotFound, "Destination landing station not found.");

            if (destination.Status != "OPERATIONAL")
                throw FieldError("destinationStationId", $"Destination landing station is not operational (Status: {destination.Status}).");

            if (request.DeliveryAddressId.HasValue)
            {
                var address = await _db.CustomerAddresses.FindAsync(request.DeliveryAddressId.Value);
                if (address == null || address.UserId != customerId)
                    throw new ApiException(StatusCodes.Status404NotFound, "Delivery address not found.");
            }

            var trackingNumber = await GenerateUniqueTrackingNumberAsync();
            var secureCode = GenerateSecurePickupCode();
            var now = DateTime.UtcNow;

            var order = new DeliveryOrder
            {
                Id = Guid.NewGuid(),
                TrackingNumber = trackingNumber,
                CustomerId = customerId,
                PackageId = request.PackageId,
                OriginStationId = request.OriginStationId,
                DestinationStationId = request.DestinationStationId,
                DeliveryAddressId = request.DeliveryAddressId,
                Status = "PENDING",
                PickupSecureCode = secureCode,
                RecipientName = request.RecipientName.Trim(),
                RecipientPhone = request.RecipientPhone.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            var initialStatusLog = new DeliveryStatusLog
            {
                OrderId = order.Id,
                ChangedBy = customerId,
                FromStatus = null,
                ToStatus = "PENDING",
                Remarks = "Delivery order created by customer.",
                CreatedAt = now
            };

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = customerId,
                OrderId = order.Id,
                Type = "ORDER_STATUS_CHANGED",
                Title = "Order Created",
                Message = $"Your delivery order {trackingNumber} has been submitted and is pending approval.",
                IsRead = false,
                CreatedAt = now
            };

            _db.DeliveryOrders.Add(order);
            _db.DeliveryStatusLogs.Add(initialStatusLog);
            _db.Notifications.Add(notification);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                throw new ApiException(StatusCodes.Status409Conflict, "This package is already linked to a delivery order.");
            }

            return new CreatedDeliveryOrderDto(
                order.Id,
                order.TrackingNumber,
                order.Status,
                order.PickupSecureCode,
                order.CustomerId,
                order.PackageId,
                order.OriginStationId,
                order.DestinationStationId,
                order.DeliveryAddressId,
                order.RecipientName,
                order.RecipientPhone,
                order.CreatedAt,
                order.UpdatedAt,
                [
                    new CreatedOrderStatusLogDto(
                        initialStatusLog.Id,
                        initialStatusLog.OrderId,
                        initialStatusLog.FromStatus,
                        initialStatusLog.ToStatus,
                        initialStatusLog.Remarks,
                        initialStatusLog.ChangedBy,
                        initialStatusLog.CreatedAt)
                ]);
        }
        finally
        {
            packageLock.Release();
        }
    }

    private async Task EnsureActiveCustomerAsync(Guid userId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");

        if (user.Status != "ACTIVE")
            throw new ApiException(StatusCodes.Status403Forbidden, "This account is not active.");
    }

    private static void ValidateCreateRequestFields(CreateDeliveryOrderRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.PackageId == Guid.Empty)
            errors["packageId"] = ["Package ID must be a non-empty UUID."];

        if (request.OriginStationId == Guid.Empty)
            errors["originStationId"] = ["Origin station ID must be a non-empty UUID."];

        if (request.DestinationStationId == Guid.Empty)
            errors["destinationStationId"] = ["Destination station ID must be a non-empty UUID."];

        if (request.OriginStationId != Guid.Empty &&
            request.DestinationStationId != Guid.Empty &&
            request.OriginStationId == request.DestinationStationId)
        {
            errors["destinationStationId"] = ["Origin and destination landing stations must be different."];
        }

        if (request.DeliveryAddressId.HasValue && request.DeliveryAddressId.Value == Guid.Empty)
            errors["deliveryAddressId"] = ["Delivery address ID must be a non-empty UUID when provided."];

        if (string.IsNullOrWhiteSpace(request.RecipientName))
            errors["recipientName"] = ["Recipient name is required."];
        else if (request.RecipientName.Trim().Length > 150)
            errors["recipientName"] = ["Recipient name must be at most 150 characters."];

        if (string.IsNullOrWhiteSpace(request.RecipientPhone) || !PhoneRegex.IsMatch(request.RecipientPhone.Trim()))
            errors["recipientPhone"] = ["Recipient phone must be 9 to 11 digits."];

        if (errors.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "One or more fields are invalid.", errors);
    }

    private static void ValidatePackageLimitsForOrder(Package package)
    {
        var errors = new Dictionary<string, string[]>();

        if (package.WeightGram <= 0m || package.WeightGram > 3000m)
            errors["weightGram"] = ["Weight must be greater than 0 and at most 3000 grams."];

        if (package.LengthCm <= 0m || package.LengthCm > 30m)
            errors["lengthCm"] = ["Length must be greater than 0 and at most 30 cm."];

        if (package.WidthCm <= 0m || package.WidthCm > 20m)
            errors["widthCm"] = ["Width must be greater than 0 and at most 20 cm."];

        if (package.HeightCm <= 0m || package.HeightCm > 15m)
            errors["heightCm"] = ["Height must be greater than 0 and at most 15 cm."];

        if (errors.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "Package exceeds drone cargo limits.", errors);
    }

    private static ApiException FieldError(string field, string message) =>
        new(StatusCodes.Status400BadRequest, "One or more fields are invalid.",
            new Dictionary<string, string[]> { [field] = [message] });

    private async Task<string> GenerateUniqueTrackingNumberAsync()
    {
        for (var i = 0; i < 10; i++)
        {
            var date = DateTime.UtcNow.ToString("yyyyMM");
            var randDigits = RandomNumberGenerator.GetInt32(0, 10000).ToString("D4");
            var tracking = $"DD-{date}-{randDigits}";

            if (!await _db.DeliveryOrders.AnyAsync(o => o.TrackingNumber == tracking))
                return tracking;
        }

        return $"DD-{DateTime.UtcNow:yyyyMM}-{RandomNumberGenerator.GetInt32(10000, 99999)}";
    }

    private static string GenerateSecurePickupCode()
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000);
        return code.ToString("D6");
    }
}
