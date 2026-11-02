using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

public interface IDeliveryOrderMutationService
{
    Task<DeliveryOrderDetailDto> UpdateAsync(Guid orderId, Guid customerId, UpdateDeliveryOrderRequest request);
    Task<DeliveryOrderDetailDto> CancelAsync(Guid orderId, Guid customerId, CancelDeliveryOrderRequest request);
}

public class DeliveryOrderMutationService : IDeliveryOrderMutationService
{
    private static readonly Regex PhoneRegex = new(@"^\d{9,11}$", RegexOptions.Compiled);

    private readonly AppDbContext _db;

    public DeliveryOrderMutationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DeliveryOrderDetailDto> UpdateAsync(Guid orderId, Guid customerId, UpdateDeliveryOrderRequest request)
    {
        await EnsureActiveCustomerAsync(customerId);
        ValidateUpdateRequestFields(request);

        var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Delivery order not found.");

        if (order.Status != "PENDING")
            throw CannotUpdateStatus(order.Status);

        var destId = request.DestinationStationId!.Value;
        if (destId == order.OriginStationId)
            throw FieldError("destinationStationId", "Origin and destination landing stations must be different.");

        var dest = await _db.LandingStations.AsNoTracking().FirstOrDefaultAsync(s => s.Id == destId)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Destination landing station not found.");

        if (dest.Status != "OPERATIONAL")
            throw FieldError("destinationStationId", $"Destination landing station is not operational (Status: {dest.Status}).");

        Guid? newAddressId = null;
        if (request.DeliveryAddressId.HasValue)
        {
            var address = await _db.CustomerAddresses.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.DeliveryAddressId.Value && a.UserId == customerId)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "Delivery address not found.");

            newAddressId = address.Id;
        }

        var recipientName = request.RecipientName!.Trim();
        var recipientPhone = request.RecipientPhone!.Trim();
        var now = DateTime.UtcNow;

        if (_db.Database.IsRelational())
        {
            var affectedRows = await _db.DeliveryOrders
                .Where(o => o.Id == orderId && o.CustomerId == customerId && o.Status == "PENDING")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(o => o.DestinationStationId, destId)
                    .SetProperty(o => o.DeliveryAddressId, newAddressId)
                    .SetProperty(o => o.RecipientName, recipientName)
                    .SetProperty(o => o.RecipientPhone, recipientPhone)
                    .SetProperty(o => o.UpdatedAt, now));

            if (affectedRows == 0)
            {
                var currentStatus = await _db.DeliveryOrders.AsNoTracking()
                    .Where(o => o.Id == orderId)
                    .Select(o => o.Status)
                    .FirstOrDefaultAsync() ?? "MODIFIED";
                throw CannotUpdateStatus(currentStatus);
            }
        }
        else
        {
            order.DestinationStationId = destId;
            order.DeliveryAddressId = newAddressId;
            order.RecipientName = recipientName;
            order.RecipientPhone = recipientPhone;
            order.UpdatedAt = now;

            try
            {
                var affectedRows = await _db.SaveChangesAsync();
                if (affectedRows == 0)
                    throw CannotUpdateStatus("MODIFIED");
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();
                var currentStatus = await _db.DeliveryOrders.AsNoTracking()
                    .Where(o => o.Id == orderId)
                    .Select(o => o.Status)
                    .FirstOrDefaultAsync() ?? "MODIFIED";
                throw CannotUpdateStatus(currentStatus);
            }
        }

        return await LoadDetailDtoAsync(orderId);
    }

    public async Task<DeliveryOrderDetailDto> CancelAsync(Guid orderId, Guid customerId, CancelDeliveryOrderRequest request)
    {
        await EnsureActiveCustomerAsync(customerId);

        if (request == null || string.IsNullOrWhiteSpace(request.CancellationReason))
            throw FieldError("cancellationReason", "Cancellation reason is required.");

        var trimmedReason = request.CancellationReason.Trim();
        if (trimmedReason.Length > 500)
            throw FieldError("cancellationReason", "Cancellation reason must be at most 500 characters.");

        var order = await _db.DeliveryOrders.FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Delivery order not found.");

        if (order.Status is not ("PENDING" or "APPROVED"))
            throw CannotCancelStatus();

        var previousStatus = order.Status;
        var now = DateTime.UtcNow;

        if (_db.Database.IsRelational())
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            var affectedRows = await _db.DeliveryOrders
                .Where(o => o.Id == orderId && o.CustomerId == customerId && o.Status == previousStatus)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(o => o.Status, "CANCELLED")
                    .SetProperty(o => o.CancelReason, trimmedReason)
                    .SetProperty(o => o.UpdatedAt, now));

            if (affectedRows == 0)
            {
                await tx.RollbackAsync();
                throw CannotCancelStatus();
            }

            _db.DeliveryStatusLogs.Add(new DeliveryStatusLog
            {
                OrderId = order.Id,
                ChangedBy = customerId,
                FromStatus = previousStatus,
                ToStatus = "CANCELLED",
                Remarks = trimmedReason,
                CreatedAt = now
            });

            _db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = order.CustomerId,
                OrderId = order.Id,
                Type = "ORDER_STATUS_CHANGED",
                Title = "Order Cancelled",
                Message = $"Your delivery order {order.TrackingNumber} has been cancelled.",
                IsRead = false,
                CreatedAt = now
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        else
        {
            order.Status = "CANCELLED";
            order.CancelReason = trimmedReason;
            order.UpdatedAt = now;

            try
            {
                var affectedRows = await _db.SaveChangesAsync();
                if (affectedRows == 0)
                    throw CannotCancelStatus();
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();
                throw CannotCancelStatus();
            }

            _db.DeliveryStatusLogs.Add(new DeliveryStatusLog
            {
                OrderId = order.Id,
                ChangedBy = customerId,
                FromStatus = previousStatus,
                ToStatus = "CANCELLED",
                Remarks = trimmedReason,
                CreatedAt = now
            });

            _db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = order.CustomerId,
                OrderId = order.Id,
                Type = "ORDER_STATUS_CHANGED",
                Title = "Order Cancelled",
                Message = $"Your delivery order {order.TrackingNumber} has been cancelled.",
                IsRead = false,
                CreatedAt = now
            });

            await _db.SaveChangesAsync();
        }

        return await LoadDetailDtoAsync(orderId);
    }

    private async Task<DeliveryOrderDetailDto> LoadDetailDtoAsync(Guid orderId)
    {
        var order = await _db.DeliveryOrders
            .AsNoTracking()
            .Include(o => o.Package)
            .Include(o => o.OriginStation)
            .Include(o => o.DestinationStation)
            .Include(o => o.DeliveryAddress)
            .Include(o => o.StatusLogs)
            .FirstAsync(o => o.Id == orderId);

        var logs = order.StatusLogs
            .OrderBy(l => l.CreatedAt)
            .ThenBy(l => l.Id)
            .Select(l => new StatusLogDto(l.FromStatus, l.ToStatus, l.Remarks, Utc(l.CreatedAt)))
            .ToList();

        return new DeliveryOrderDetailDto(
            order.Id,
            order.TrackingNumber,
            order.Status,
            order.RecipientName,
            order.RecipientPhone,
            order.Package.Id,
            order.Package.PackageName,
            order.OriginStation.Id,
            order.OriginStation.Name,
            order.DestinationStation.Id,
            order.DestinationStation.Name,
            order.DeliveryAddressId,
            ToPackageDto(order.Package),
            StationSummaryDto.From(order.OriginStation),
            StationSummaryDto.From(order.DestinationStation),
            order.DeliveryAddress is null ? null : ToAddressDto(order.DeliveryAddress),
            order.CancelReason,
            order.ScheduledDepartureTime,
            order.ActualDepartureTime,
            order.EstimatedDeliveryTime,
            order.ActualDeliveryTime,
            null,
            logs,
            Utc(order.CreatedAt),
            Utc(order.UpdatedAt));
    }

    private async Task EnsureActiveCustomerAsync(Guid customerId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == customerId)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");

        if (user.Status != "ACTIVE")
            throw new ApiException(StatusCodes.Status403Forbidden, "This account is not active.");
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
            errors["destinationStationId"] = ["Destination station ID must be a non-empty UUID."];

        if (request.DeliveryAddressId.HasValue && request.DeliveryAddressId.Value == Guid.Empty)
            errors["deliveryAddressId"] = ["Delivery address ID must be a non-empty UUID when provided."];

        if (errors.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "One or more fields are invalid.", errors);
    }

    private static ApiException FieldError(string field, string message) =>
        new(StatusCodes.Status400BadRequest, "One or more fields are invalid.",
            new Dictionary<string, string[]> { [field] = [message] });

    private static ApiException CannotUpdateStatus(string status) =>
        new(StatusCodes.Status409Conflict, $"Order can only be updated while in PENDING status (Current status: '{status}').");

    private static ApiException CannotCancelStatus() =>
        new(StatusCodes.Status409Conflict, "Only PENDING or APPROVED orders can be cancelled.");

    private static PackageDto ToPackageDto(Package p) => new(
        p.Id, p.SenderId, p.PackageName, p.WeightGram,
        p.LengthCm, p.WidthCm, p.HeightCm, p.PackageImageUrl, p.IsFragile, Utc(p.CreatedAt));

    private static CustomerAddressDto ToAddressDto(CustomerAddress a) => new(
        a.Id, a.UserId, a.Label, a.ContactName,
        a.ContactPhone, a.AddressLine, a.Latitude, a.Longitude, a.IsDefault, Utc(a.CreatedAt), Utc(a.UpdatedAt));

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
