using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

public interface IDeliveryOrderService
{
    Task<CreatedDeliveryOrderDto> CreateAsync(Guid customerId, CreateDeliveryOrderRequest request);
}

/// <summary>Customer delivery orders (LA-103, contract section 5.1).</summary>
public class DeliveryOrderService : IDeliveryOrderService
{
    // Drone cargo limits (BR-CO-06, BR-CO-07).
    private const decimal MaxWeightGram = 3000m;
    private const decimal MaxLengthCm = 30m;
    private const decimal MaxWidthCm = 20m;
    private const decimal MaxHeightCm = 15m;

    // A tracking number can collide with a concurrent request; retry before answering 409.
    private const int MaxSaveAttempts = 5;

    private readonly AppDbContext _db;

    public DeliveryOrderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CreatedDeliveryOrderDto> CreateAsync(Guid customerId, CreateDeliveryOrderRequest request)
    {
        await EnsureActiveCustomerAsync(customerId);
        ValidateFields(request);

        // Not yours = not found, so ownership is not revealed (BR-CO-36).
        var package = await _db.Packages.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PackageId && p.SenderId == customerId)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Package not found.");

        ValidatePackageLimits(package);

        // BR-CO-08: one package, one order, never reused, even after the order is cancelled.
        if (await _db.DeliveryOrders.AnyAsync(o => o.PackageId == package.Id))
            throw PackageAlreadyLinked();

        var origin = await GetOperationalStationAsync(request.OriginStationId, "originStationId", "Origin");
        var destination = await GetOperationalStationAsync(request.DestinationStationId, "destinationStationId", "Destination");

        CustomerAddress? address = null;
        if (request.DeliveryAddressId is { } addressId)
        {
            address = await _db.CustomerAddresses.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == customerId)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "Delivery address not found.");
        }

        for (var attempt = 1; ; attempt++)
        {
            var now = DateTime.UtcNow;
            var order = new DeliveryOrder
            {
                Id = Guid.NewGuid(),
                TrackingNumber = await NextTrackingNumberAsync(now),
                CustomerId = customerId,
                PackageId = package.Id,
                OriginStationId = origin.Id,
                DestinationStationId = destination.Id,
                DeliveryAddressId = address?.Id,
                RecipientName = request.RecipientName.Trim(),
                RecipientPhone = request.RecipientPhone.Trim(),
                Status = "PENDING",
                PickupSecureCode = GeneratePickupCode(),
                CreatedAt = now,
                UpdatedAt = now
            };
            var log = new DeliveryStatusLog
            {
                OrderId = order.Id,
                ChangedBy = customerId,
                FromStatus = null,
                ToStatus = "PENDING",
                Remarks = "Order created",
                CreatedAt = now
            };
            var notification = new Notification
            {
                UserId = customerId,
                OrderId = order.Id,
                Type = "ORDER_STATUS_CHANGED",
                Title = "Order created",
                Message = $"Your delivery order {order.TrackingNumber} was created and is waiting for review.",
                CreatedAt = now
            };

            // One SaveChanges is one transaction: order, first status log and notification, or nothing.
            _db.DeliveryOrders.Add(order);
            _db.DeliveryStatusLogs.Add(log);
            _db.Notifications.Add(notification);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();

                // Another request linked this package first (unique index on package id).
                if (await _db.DeliveryOrders.AnyAsync(o => o.PackageId == package.Id))
                    throw PackageAlreadyLinked();

                if (attempt >= MaxSaveAttempts)
                    throw new ApiException(StatusCodes.Status409Conflict,
                        "The tracking number collided with another order. Please try again.");

                continue;
            }

            var detail = ToDetail(order, package, origin, destination, address, [new StatusLogDto(null, "PENDING", log.Remarks, log.CreatedAt)]);
            return new CreatedDeliveryOrderDto(detail, order.PickupSecureCode);
        }
    }

    private async Task EnsureActiveCustomerAsync(Guid customerId)
    {
        var status = await _db.Users.AsNoTracking()
            .Where(u => u.Id == customerId)
            .Select(u => u.Status)
            .FirstOrDefaultAsync()
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");

        if (status != "ACTIVE")
            throw new ApiException(StatusCodes.Status403Forbidden, "This account is not active.");
    }

    private static void ValidateFields(CreateDeliveryOrderRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.PackageId == Guid.Empty)
            errors["packageId"] = ["Package is required."];
        if (request.OriginStationId == Guid.Empty)
            errors["originStationId"] = ["Origin station is required."];
        if (request.DestinationStationId == Guid.Empty)
            errors["destinationStationId"] = ["Destination station is required."];
        else if (request.DestinationStationId == request.OriginStationId)
            errors["destinationStationId"] = ["Destination station must be different from the origin station."];
        if (string.IsNullOrWhiteSpace(request.RecipientName))
            errors["recipientName"] = ["Recipient name is required."];

        if (errors.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "One or more fields are invalid.", errors);
    }

    private static void ValidatePackageLimits(Package package)
    {
        var errors = new Dictionary<string, string[]>();

        if (package.WeightGram <= 0 || package.WeightGram > MaxWeightGram)
            errors["weightGram"] = [$"Weight must be greater than 0 and at most {MaxWeightGram:0} grams."];
        if (package.LengthCm <= 0 || package.LengthCm > MaxLengthCm)
            errors["lengthCm"] = [$"Length must be greater than 0 and at most {MaxLengthCm:0} cm."];
        if (package.WidthCm <= 0 || package.WidthCm > MaxWidthCm)
            errors["widthCm"] = [$"Width must be greater than 0 and at most {MaxWidthCm:0} cm."];
        if (package.HeightCm <= 0 || package.HeightCm > MaxHeightCm)
            errors["heightCm"] = [$"Height must be greater than 0 and at most {MaxHeightCm:0} cm."];

        if (errors.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "The package is over the drone limits.", errors);
    }

    private async Task<LandingStation> GetOperationalStationAsync(Guid id, string field, string label)
    {
        var station = await _db.LandingStations.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new ApiException(StatusCodes.Status404NotFound, $"{label} station not found.");

        if (station.Status != "OPERATIONAL")
            throw new ApiException(StatusCodes.Status400BadRequest, "One or more fields are invalid.",
                new Dictionary<string, string[]> { [field] = [$"{label} station is not operational."] });

        return station;
    }

    /// <summary>Format DD-YYYYMM-NNNN: the next number of the month, starting at 0001.</summary>
    private async Task<string> NextTrackingNumberAsync(DateTime now)
    {
        var prefix = $"DD-{now.ToString("yyyyMM", CultureInfo.InvariantCulture)}-";
        var latest = await _db.DeliveryOrders.AsNoTracking()
            .Where(o => o.TrackingNumber.StartsWith(prefix))
            .Select(o => o.TrackingNumber)
            .OrderByDescending(t => t)
            .FirstOrDefaultAsync();

        var next = latest is not null && int.TryParse(latest[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? n + 1
            : 1;
        return $"{prefix}{next:D4}";
    }

    /// <summary>Exactly 6 digits; leading zeros are allowed.</summary>
    private static string GeneratePickupCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    private static DeliveryOrderDetailDto ToDetail(DeliveryOrder o, Package package, LandingStation origin,
        LandingStation destination, CustomerAddress? address, IReadOnlyList<StatusLogDto> logs) => new(
        o.Id, o.TrackingNumber, o.Status, o.RecipientName, o.RecipientPhone,
        package.Id, package.PackageName,
        origin.Id, origin.Name, destination.Id, destination.Name,
        o.DeliveryAddressId,
        ToPackageDto(package), StationSummaryDto.From(origin), StationSummaryDto.From(destination),
        address is null ? null : ToAddressDto(address),
        o.CancelReason, o.ScheduledDepartureTime, o.ActualDepartureTime, o.EstimatedDeliveryTime, o.ActualDeliveryTime,
        null, logs,
        DateTime.SpecifyKind(o.CreatedAt, DateTimeKind.Utc), DateTime.SpecifyKind(o.UpdatedAt, DateTimeKind.Utc));

    // Same DTOs as the package and address endpoints, so the nested objects look the same everywhere.
    private static PackageDto ToPackageDto(Package p) => new(p.Id, p.SenderId, p.PackageName, p.WeightGram,
        p.LengthCm, p.WidthCm, p.HeightCm, p.PackageImageUrl, p.IsFragile, Utc(p.CreatedAt));

    private static CustomerAddressDto ToAddressDto(CustomerAddress a) => new(a.Id, a.UserId, a.Label, a.ContactName,
        a.ContactPhone, a.AddressLine, a.Latitude, a.Longitude, a.IsDefault, Utc(a.CreatedAt), Utc(a.UpdatedAt));

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static ApiException PackageAlreadyLinked() =>
        new(StatusCodes.Status409Conflict, "This package is already linked to a delivery order.");
}
