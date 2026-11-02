using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public interface IDeliveryOrderService
{
    Task<DeliveryOrderDetailDto> CreateAsync(Guid customerId, CreateDeliveryOrderRequest request);
    Task<DeliveryOrderDetailDto> UpdateAsync(Guid orderId, Guid userId, string userRole, UpdateDeliveryOrderRequest request);
    Task<DeliveryOrderDetailDto> CancelAsync(Guid orderId, Guid userId, string userRole, CancelDeliveryOrderRequest request);
    Task<PagedResult<DeliveryOrderSummaryDto>> ListAsync(Guid userId, string userRole, DeliveryOrderFilterQuery query);
    Task<DeliveryOrderDetailDto> GetByIdAsync(Guid orderId, Guid userId, string userRole);
    Task<IReadOnlyList<DeliveryStatusLogDto>> GetStatusHistoryAsync(Guid orderId, Guid userId, string userRole);
}

public class DeliveryOrderService : IDeliveryOrderService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> OrderLocks = new();
    private static readonly Regex PhoneRegex = new(@"^\d{9,11}$", RegexOptions.Compiled);
    private static readonly HashSet<string> ValidOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "PENDING", "APPROVED", "READY_FOR_TAKEOFF", "IN_TRANSIT", "ARRIVED", "DELIVERED", "CANCELLED", "FAILED"
    };

    private readonly AppDbContext _db;

    public DeliveryOrderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DeliveryOrderDetailDto> CreateAsync(Guid customerId, CreateDeliveryOrderRequest request)
    {
        await EnsureActiveUserAsync(customerId);
        ValidateCreateRequestFields(request);

        var packageLock = OrderLocks.GetOrAdd(request.PackageId, _ => new SemaphoreSlim(1, 1));
        await packageLock.WaitAsync();
        try
        {
            // 1. Validate package exists and belongs to customer (BR-CO-36: not yours = 404)
            var package = await _db.Packages
                .Include(p => p.DeliveryOrder)
                .FirstOrDefaultAsync(p => p.Id == request.PackageId);

            if (package == null || package.SenderId != customerId)
                throw new ApiException(StatusCodes.Status404NotFound, "Package not found.");

            // BR-CO-08: one package, one order, never reused even if the order is cancelled
            if (package.DeliveryOrder != null)
                throw new ApiException(StatusCodes.Status409Conflict, "This package is already linked to a delivery order.");

            // Re-check drone cargo limits when creating an order (BR-CO-06, BR-CO-07)
            ValidatePackageLimitsForOrder(package);

            // 2. Validate Origin and Destination stations
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

            // 3. Validate Delivery Address if provided (not found or not yours = 404)
            if (request.DeliveryAddressId.HasValue)
            {
                var address = await _db.CustomerAddresses.FindAsync(request.DeliveryAddressId.Value);
                if (address == null || address.UserId != customerId)
                    throw new ApiException(StatusCodes.Status404NotFound, "Delivery address not found.");
            }

            // 4. Generate unique tracking number and secure code
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
                RecipientName = request.RecipientName.Trim(),
                RecipientPhone = request.RecipientPhone.Trim(),
                Status = "PENDING",
                PickupSecureCode = secureCode,
                CreatedAt = now,
                UpdatedAt = now
            };

            var initialStatusLog = new DeliveryStatusLog
            {
                OrderId = order.Id,
                ChangedBy = customerId,
                FromStatus = null,
                ToStatus = "PENDING",
                Remarks = "Order created by customer.",
                CreatedAt = now
            };

            // FR-12: Create notification for the customer
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = customerId,
                OrderId = order.Id,
                Type = "ORDER_STATUS_CHANGED",
                Title = "Order Created",
                Message = $"Your delivery order #{trackingNumber} has been created and is waiting for review.",
                IsRead = false,
                CreatedAt = now
            };

            _db.DeliveryOrders.Add(order);
            _db.DeliveryStatusLogs.Add(initialStatusLog);
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            // Only POST /api/delivery-orders includes pickupSecureCode
            return (await GetByIdInternalAsync(order.Id, includePickupCode: true))!;
        }
        finally
        {
            packageLock.Release();
        }
    }

    public async Task<DeliveryOrderDetailDto> UpdateAsync(Guid orderId, Guid userId, string userRole, UpdateDeliveryOrderRequest request)
    {
        await EnsureActiveUserAsync(userId);
        ValidateUpdateRequestFields(request);

        var orderLock = OrderLocks.GetOrAdd(orderId, _ => new SemaphoreSlim(1, 1));
        await orderLock.WaitAsync();
        try
        {
            var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || order.CustomerId != userId)
                throw new ApiException(StatusCodes.Status404NotFound, "Delivery order not found.");

            if (order.Status != "PENDING")
                throw new ApiException(StatusCodes.Status409Conflict,
                    $"Order can only be updated while status is 'PENDING'. Current status is '{order.Status}'.");

            if (request.DestinationStationId.HasValue)
            {
                if (request.DestinationStationId.Value == order.OriginStationId)
                    throw FieldError("destinationStationId", "Origin and destination stations must be different.");

                var destination = await _db.LandingStations.FindAsync(request.DestinationStationId.Value);
                if (destination == null)
                    throw new ApiException(StatusCodes.Status404NotFound, "Destination landing station not found.");

                if (destination.Status != "OPERATIONAL")
                    throw FieldError("destinationStationId", $"Destination landing station is not operational (Status: {destination.Status}).");

                order.DestinationStationId = request.DestinationStationId.Value;
            }

            if (request.DeliveryAddressId.HasValue)
            {
                var address = await _db.CustomerAddresses.FindAsync(request.DeliveryAddressId.Value);
                if (address == null || address.UserId != order.CustomerId)
                    throw new ApiException(StatusCodes.Status404NotFound, "Delivery address not found.");
                order.DeliveryAddressId = request.DeliveryAddressId.Value;
            }
            else
            {
                // Allow clearing deliveryAddressId by sending null
                order.DeliveryAddressId = null;
            }

            if (!string.IsNullOrWhiteSpace(request.RecipientName))
                order.RecipientName = request.RecipientName.Trim();

            if (!string.IsNullOrWhiteSpace(request.RecipientPhone))
                order.RecipientPhone = request.RecipientPhone.Trim();

            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return (await GetByIdInternalAsync(order.Id, includePickupCode: false))!;
        }
        finally
        {
            orderLock.Release();
        }
    }

    public async Task<DeliveryOrderDetailDto> CancelAsync(Guid orderId, Guid userId, string userRole, CancelDeliveryOrderRequest request)
    {
        await EnsureActiveUserAsync(userId);

        var reasonText = request.EffectiveReason?.Trim();
        if (string.IsNullOrWhiteSpace(reasonText))
            throw FieldError("cancellationReason", "Cancellation reason is required.");
        if (reasonText.Length > 500)
            throw FieldError("cancellationReason", "Cancellation reason must be at most 500 characters.");

        var orderLock = OrderLocks.GetOrAdd(orderId, _ => new SemaphoreSlim(1, 1));
        await orderLock.WaitAsync();
        try
        {
            var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || order.CustomerId != userId)
                throw new ApiException(StatusCodes.Status404NotFound, "Delivery order not found.");

            if (order.Status is not ("PENDING" or "APPROVED"))
                throw new ApiException(StatusCodes.Status409Conflict,
                    "Only PENDING or APPROVED orders can be cancelled.");

            var oldStatus = order.Status;
            var now = DateTime.UtcNow;

            order.Status = "CANCELLED";
            order.CancelReason = reasonText;
            order.UpdatedAt = now;

            var statusLog = new DeliveryStatusLog
            {
                OrderId = order.Id,
                ChangedBy = userId,
                FromStatus = oldStatus,
                ToStatus = "CANCELLED",
                Remarks = reasonText,
                CreatedAt = now
            };

            // FR-12: Create notification for the customer
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = order.CustomerId,
                OrderId = order.Id,
                Type = "ORDER_STATUS_CHANGED",
                Title = "Order Cancelled",
                Message = $"Your delivery order #{order.TrackingNumber} has been cancelled. Reason: {reasonText}",
                IsRead = false,
                CreatedAt = now
            };

            _db.DeliveryStatusLogs.Add(statusLog);
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            return (await GetByIdInternalAsync(order.Id, includePickupCode: false))!;
        }
        finally
        {
            orderLock.Release();
        }
    }

    public async Task<PagedResult<DeliveryOrderSummaryDto>> ListAsync(Guid userId, string userRole, DeliveryOrderFilterQuery query)
    {
        await EnsureActiveUserAsync(userId);
        ValidateFilterQuery(query);

        // Contract Section 1 & 5.2: Customer only sees their own orders
        var queryable = _db.DeliveryOrders
            .AsNoTracking()
            .Where(o => o.CustomerId == userId);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var normalizedStatus = query.Status.Trim().ToUpperInvariant();
            queryable = queryable.Where(o => o.Status == normalizedStatus);
        }

        var keyword = !string.IsNullOrWhiteSpace(query.TrackingNumber)
            ? query.TrackingNumber.Trim()
            : query.SearchKeyword?.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lowerKeyword = keyword.ToLowerInvariant();
            queryable = queryable.Where(o => o.TrackingNumber.ToLower().Contains(lowerKeyword));
        }

        if (query.FromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(query.FromDate.Value, DateTimeKind.Utc);
            queryable = queryable.Where(o => o.CreatedAt >= fromUtc);
        }

        if (query.ToDate.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(query.ToDate.Value, DateTimeKind.Utc);
            queryable = queryable.Where(o => o.CreatedAt <= toUtc);
        }

        var totalCount = await queryable.CountAsync();

        var page = query.Page;
        var pageSize = query.PageSize;

        var items = await queryable
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new DeliveryOrderSummaryDto(
                o.Id,
                o.TrackingNumber,
                o.Status,
                o.CustomerId,
                o.Customer.FullName,
                o.PackageId,
                o.Package.PackageName,
                o.Package.WeightGram,
                o.OriginStationId,
                o.OriginStation.Name,
                o.DestinationStationId,
                o.DestinationStation.Name,
                o.RecipientName,
                o.RecipientPhone,
                o.CreatedAt,
                o.UpdatedAt,
                o.DeliveryAddressId))
            .ToListAsync();

        return new PagedResult<DeliveryOrderSummaryDto>(items, totalCount, page, pageSize);
    }

    public async Task<DeliveryOrderDetailDto> GetByIdAsync(Guid orderId, Guid userId, string userRole)
    {
        await EnsureActiveUserAsync(userId);

        var dto = await GetByIdInternalAsync(orderId, includePickupCode: false);
        if (dto == null || dto.CustomerId != userId)
            throw new ApiException(StatusCodes.Status404NotFound, "Delivery order not found.");

        return dto;
    }

    public async Task<IReadOnlyList<DeliveryStatusLogDto>> GetStatusHistoryAsync(Guid orderId, Guid userId, string userRole)
    {
        await EnsureActiveUserAsync(userId);

        var order = await _db.DeliveryOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null || order.CustomerId != userId)
            throw new ApiException(StatusCodes.Status404NotFound, "Delivery order not found.");

        return await _db.DeliveryStatusLogs
            .AsNoTracking()
            .Where(l => l.OrderId == orderId)
            .OrderBy(l => l.CreatedAt)
            .Select(l => new DeliveryStatusLogDto(
                l.Id,
                l.OrderId,
                l.FromStatus,
                l.ToStatus,
                l.Remarks,
                l.ChangedBy,
                l.ChangedByUser != null ? l.ChangedByUser.FullName : null,
                l.CreatedAt))
            .ToListAsync();
    }

    private async Task EnsureActiveUserAsync(Guid userId)
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
            errors["packageId"] = ["Package ID is required."];

        if (request.OriginStationId == Guid.Empty)
            errors["originStationId"] = ["Origin station ID is required."];

        if (request.DestinationStationId == Guid.Empty)
            errors["destinationStationId"] = ["Destination station ID is required."];
        else if (request.OriginStationId == request.DestinationStationId)
            errors["destinationStationId"] = ["Origin and destination stations must be different."];

        if (string.IsNullOrWhiteSpace(request.RecipientName))
            errors["recipientName"] = ["Recipient name is required."];
        else if (request.RecipientName.Trim().Length > 150)
            errors["recipientName"] = ["Recipient name must be at most 150 characters."];

        if (string.IsNullOrWhiteSpace(request.RecipientPhone) || !PhoneRegex.IsMatch(request.RecipientPhone.Trim()))
            errors["recipientPhone"] = ["Recipient phone must be 9 to 11 digits."];

        if (errors.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "One or more fields are invalid.", errors);
    }

    private static void ValidateUpdateRequestFields(UpdateDeliveryOrderRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.RecipientName))
            errors["recipientName"] = ["Recipient name is required."];
        else if (request.RecipientName.Trim().Length > 150)
            errors["recipientName"] = ["Recipient name must be at most 150 characters."];

        if (string.IsNullOrWhiteSpace(request.RecipientPhone) || !PhoneRegex.IsMatch(request.RecipientPhone.Trim()))
            errors["recipientPhone"] = ["Recipient phone must be 9 to 11 digits."];

        if (!request.DestinationStationId.HasValue || request.DestinationStationId.Value == Guid.Empty)
            errors["destinationStationId"] = ["Destination station ID is required."];

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

    private static void ValidateFilterQuery(DeliveryOrderFilterQuery query)
    {
        var errors = new Dictionary<string, string[]>();

        if (query.Page < 1)
            errors["page"] = ["Page must be at least 1."];

        if (query.PageSize is < 1 or > 100)
            errors["pageSize"] = ["PageSize must be between 1 and 100."];

        if (!string.IsNullOrWhiteSpace(query.Status) && !ValidOrderStatuses.Contains(query.Status.Trim()))
            errors["status"] = ["Invalid order status filter."];

        if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate.Value > query.ToDate.Value)
            errors["fromDate"] = ["fromDate must not be later than toDate."];

        var keyword = !string.IsNullOrWhiteSpace(query.TrackingNumber)
            ? query.TrackingNumber
            : query.SearchKeyword;

        if (!string.IsNullOrEmpty(keyword) && keyword.Length > 50)
            errors["searchKeyword"] = ["Search keyword must be at most 50 characters."];

        if (errors.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "One or more query parameters are invalid.", errors);
    }

    private static ApiException FieldError(string field, string message) =>
        new(StatusCodes.Status400BadRequest, "One or more fields are invalid.",
            new Dictionary<string, string[]> { [field] = [message] });

    private async Task<DeliveryOrderDetailDto?> GetByIdInternalAsync(Guid orderId, bool includePickupCode)
    {
        return await _db.DeliveryOrders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new DeliveryOrderDetailDto(
                o.Id,
                o.TrackingNumber,
                o.Status,
                o.CancelReason,
                includePickupCode ? o.PickupSecureCode : null,
                o.CustomerId,
                o.Customer.FullName,
                o.Customer.Email,
                o.Customer.PhoneNumber,
                new PackageDto(
                    o.Package.Id,
                    o.Package.SenderId,
                    o.Package.PackageName,
                    o.Package.WeightGram,
                    o.Package.LengthCm,
                    o.Package.WidthCm,
                    o.Package.HeightCm,
                    o.Package.PackageImageUrl,
                    o.Package.IsFragile,
                    o.Package.CreatedAt),
                o.OriginStationId,
                o.OriginStation.Name,
                o.OriginStation.Address,
                o.DestinationStationId,
                o.DestinationStation.Name,
                o.DestinationStation.Address,
                o.DeliveryAddressId,
                o.DeliveryAddress != null
                    ? new CustomerAddressDto(
                        o.DeliveryAddress.Id,
                        o.DeliveryAddress.UserId,
                        o.DeliveryAddress.Label,
                        o.DeliveryAddress.ContactName,
                        o.DeliveryAddress.ContactPhone,
                        o.DeliveryAddress.AddressLine,
                        o.DeliveryAddress.Latitude,
                        o.DeliveryAddress.Longitude,
                        o.DeliveryAddress.IsDefault,
                        o.DeliveryAddress.CreatedAt,
                        o.DeliveryAddress.UpdatedAt)
                    : null,
                o.DispatcherId,
                o.Dispatcher != null ? o.Dispatcher.FullName : null,
                o.RecipientName,
                o.RecipientPhone,
                o.ScheduledDepartureTime,
                o.ActualDepartureTime,
                o.EstimatedDeliveryTime,
                o.ActualDeliveryTime,
                o.CreatedAt,
                o.UpdatedAt,
                o.StatusLogs
                    .OrderBy(l => l.CreatedAt)
                    .Select(l => new DeliveryStatusLogDto(
                        l.Id,
                        l.OrderId,
                        l.FromStatus,
                        l.ToStatus,
                        l.Remarks,
                        l.ChangedBy,
                        l.ChangedByUser != null ? l.ChangedByUser.FullName : null,
                        l.CreatedAt))
                    .ToList(),
                o.PackageId,
                o.Package.PackageName,
                new StationSummaryDto(
                    o.OriginStation.Id,
                    o.OriginStation.StationCode,
                    o.OriginStation.Name,
                    o.OriginStation.Address,
                    o.OriginStation.Latitude,
                    o.OriginStation.Longitude,
                    o.OriginStation.Status),
                new StationSummaryDto(
                    o.DestinationStation.Id,
                    o.DestinationStation.StationCode,
                    o.DestinationStation.Name,
                    o.DestinationStation.Address,
                    o.DestinationStation.Latitude,
                    o.DestinationStation.Longitude,
                    o.DestinationStation.Status),
                null))
            .FirstOrDefaultAsync();
    }

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
