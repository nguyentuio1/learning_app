using System.Security.Cryptography;
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
    private readonly AppDbContext _db;

    public DeliveryOrderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DeliveryOrderDetailDto> CreateAsync(Guid customerId, CreateDeliveryOrderRequest request)
    {
        // 1. Validate package exists and belongs to customer
        var package = await _db.Packages
            .Include(p => p.DeliveryOrder)
            .FirstOrDefaultAsync(p => p.Id == request.PackageId);

        if (package == null)
            throw new ApiException(StatusCodes.Status404NotFound, "Package not found.");

        if (package.SenderId != customerId)
            throw new ApiException(StatusCodes.Status403Forbidden, "You do not own this package.");

        if (package.DeliveryOrder != null && package.DeliveryOrder.Status != "CANCELLED")
            throw new ApiException(StatusCodes.Status409Conflict, "This package is already assigned to an active delivery order.");

        // 2. Validate Origin and Destination stations
        if (request.OriginStationId == request.DestinationStationId)
            throw new ApiException(StatusCodes.Status400BadRequest, "Origin and destination stations must be different.");

        var origin = await _db.LandingStations.FindAsync(request.OriginStationId);
        if (origin == null)
            throw new ApiException(StatusCodes.Status400BadRequest, "Origin landing station not found.");

        if (origin.Status != "OPERATIONAL")
            throw new ApiException(StatusCodes.Status400BadRequest, $"Origin landing station is not operational (Status: {origin.Status}).");

        var destination = await _db.LandingStations.FindAsync(request.DestinationStationId);
        if (destination == null)
            throw new ApiException(StatusCodes.Status400BadRequest, "Destination landing station not found.");

        if (destination.Status != "OPERATIONAL")
            throw new ApiException(StatusCodes.Status400BadRequest, $"Destination landing station is not operational (Status: {destination.Status}).");

        // 3. Validate Delivery Address if provided
        if (request.DeliveryAddressId.HasValue)
        {
            var address = await _db.CustomerAddresses.FindAsync(request.DeliveryAddressId.Value);
            if (address == null || address.UserId != customerId)
                throw new ApiException(StatusCodes.Status400BadRequest, "Delivery address not found or does not belong to you.");
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

        return (await GetByIdInternalAsync(order.Id))!;
    }

    public async Task<DeliveryOrderDetailDto> UpdateAsync(Guid orderId, Guid userId, string userRole, UpdateDeliveryOrderRequest request)
    {
        var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
            throw new ApiException(StatusCodes.Status404NotFound, "Delivery order not found.");

        if (userRole == AppRoles.Customer && order.CustomerId != userId)
            throw new ApiException(StatusCodes.Status403Forbidden, "You do not have permission to modify this order.");

        if (order.Status != "PENDING")
            throw new ApiException(StatusCodes.Status409Conflict,
                $"Order can only be updated while status is 'PENDING'. Current status is '{order.Status}'.");

        if (request.DeliveryAddressId.HasValue)
        {
            var address = await _db.CustomerAddresses.FindAsync(request.DeliveryAddressId.Value);
            if (address == null || address.UserId != order.CustomerId)
                throw new ApiException(StatusCodes.Status400BadRequest, "Invalid delivery address.");
            order.DeliveryAddressId = request.DeliveryAddressId.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.RecipientName))
            order.RecipientName = request.RecipientName.Trim();

        if (!string.IsNullOrWhiteSpace(request.RecipientPhone))
            order.RecipientPhone = request.RecipientPhone.Trim();

        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return (await GetByIdInternalAsync(order.Id))!;
    }

    public async Task<DeliveryOrderDetailDto> CancelAsync(Guid orderId, Guid userId, string userRole, CancelDeliveryOrderRequest request)
    {
        var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
            throw new ApiException(StatusCodes.Status404NotFound, "Delivery order not found.");

        if (userRole == AppRoles.Customer && order.CustomerId != userId)
            throw new ApiException(StatusCodes.Status403Forbidden, "You do not have permission to cancel this order.");

        if (order.Status is "IN_TRANSIT" or "ARRIVED" or "DELIVERED" or "CANCELLED")
            throw new ApiException(StatusCodes.Status409Conflict,
                $"Order cannot be cancelled because it is in status '{order.Status}'.");

        var oldStatus = order.Status;
        var now = DateTime.UtcNow;

        order.Status = "CANCELLED";
        order.CancelReason = request.Reason.Trim();
        order.UpdatedAt = now;

        var statusLog = new DeliveryStatusLog
        {
            OrderId = order.Id,
            ChangedBy = userId,
            FromStatus = oldStatus,
            ToStatus = "CANCELLED",
            Remarks = request.Reason.Trim(),
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
            Message = $"Your delivery order #{order.TrackingNumber} has been cancelled. Reason: {request.Reason.Trim()}",
            IsRead = false,
            CreatedAt = now
        };

        _db.DeliveryStatusLogs.Add(statusLog);
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        return (await GetByIdInternalAsync(order.Id))!;
    }

    public async Task<PagedResult<DeliveryOrderSummaryDto>> ListAsync(Guid userId, string userRole, DeliveryOrderFilterQuery query)
    {
        var queryable = _db.DeliveryOrders.AsNoTracking();

        // Customers can only see their own orders
        if (userRole == AppRoles.Customer)
        {
            queryable = queryable.Where(o => o.CustomerId == userId);
        }
        else if (query.CustomerId.HasValue)
        {
            queryable = queryable.Where(o => o.CustomerId == query.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var normalizedStatus = query.Status.Trim().ToUpperInvariant();
            queryable = queryable.Where(o => o.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(query.TrackingNumber))
        {
            var tracking = query.TrackingNumber.Trim();
            queryable = queryable.Where(o => EF.Functions.ILike(o.TrackingNumber, $"%{tracking}%"));
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

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

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
                o.UpdatedAt))
            .ToListAsync();

        return new PagedResult<DeliveryOrderSummaryDto>(items, totalCount, page, pageSize);
    }

    public async Task<DeliveryOrderDetailDto> GetByIdAsync(Guid orderId, Guid userId, string userRole)
    {
        var dto = await GetByIdInternalAsync(orderId);
        if (dto == null)
            throw new ApiException(StatusCodes.Status404NotFound, "Delivery order not found.");

        if (userRole == AppRoles.Customer && dto.CustomerId != userId)
            throw new ApiException(StatusCodes.Status403Forbidden, "You do not have permission to view this order.");

        return dto;
    }

    public async Task<IReadOnlyList<DeliveryStatusLogDto>> GetStatusHistoryAsync(Guid orderId, Guid userId, string userRole)
    {
        var order = await _db.DeliveryOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
            throw new ApiException(StatusCodes.Status404NotFound, "Delivery order not found.");

        if (userRole == AppRoles.Customer && order.CustomerId != userId)
            throw new ApiException(StatusCodes.Status403Forbidden, "You do not have permission to view this order's history.");

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

    private async Task<DeliveryOrderDetailDto?> GetByIdInternalAsync(Guid orderId)
    {
        return await _db.DeliveryOrders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new DeliveryOrderDetailDto(
                o.Id,
                o.TrackingNumber,
                o.Status,
                o.CancelReason,
                o.PickupSecureCode,
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
                    .ToList()))
            .FirstOrDefaultAsync();
    }

    private async Task<string> GenerateUniqueTrackingNumberAsync()
    {
        for (var i = 0; i < 10; i++)
        {
            var date = DateTime.UtcNow.ToString("yyyyMMdd");
            var randHex = RandomNumberGenerator.GetHexString(3).ToUpperInvariant();
            var tracking = $"DD-{date}-{randHex}";

            if (!await _db.DeliveryOrders.AnyAsync(o => o.TrackingNumber == tracking))
                return tracking;
        }

        return $"DD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }

    private static string GenerateSecurePickupCode()
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000);
        return code.ToString("D6");
    }
}
