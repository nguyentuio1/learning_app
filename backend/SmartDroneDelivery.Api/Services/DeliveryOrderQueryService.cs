using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

public interface IDeliveryOrderQueryService
{
    Task<PagedResult<DeliveryOrderSummaryDto>> ListAsync(Guid customerId, DeliveryOrderFilterQuery query);
    Task<DeliveryOrderDetailDto> GetByIdAsync(Guid orderId, Guid customerId);
}

public class DeliveryOrderQueryService : IDeliveryOrderQueryService
{
    private static readonly HashSet<string> ValidOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "PENDING", "APPROVED", "READY_FOR_TAKEOFF", "IN_TRANSIT", "ARRIVED", "DELIVERED", "CANCELLED", "FAILED"
    };

    private readonly AppDbContext _db;

    public DeliveryOrderQueryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<DeliveryOrderSummaryDto>> ListAsync(Guid customerId, DeliveryOrderFilterQuery query)
    {
        await EnsureActiveCustomerAsync(customerId);
        ValidateFilterQuery(query);

        var page = query.Page;
        var pageSize = query.PageSize;

        var dbQuery = _db.DeliveryOrders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToUpperInvariant();
            dbQuery = dbQuery.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchKeyword))
        {
            var kw = query.SearchKeyword.Trim().ToLower();
            dbQuery = dbQuery.Where(o => o.TrackingNumber.ToLower().Contains(kw));
        }

        if (query.FromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(query.FromDate.Value, DateTimeKind.Utc);
            dbQuery = dbQuery.Where(o => o.CreatedAt >= fromUtc);
        }

        if (query.ToDate.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(query.ToDate.Value, DateTimeKind.Utc);
            dbQuery = dbQuery.Where(o => o.CreatedAt <= toUtc);
        }

        var totalCount = await dbQuery.CountAsync();
        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;

        var items = await dbQuery
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new DeliveryOrderSummaryDto(
                o.Id,
                o.TrackingNumber,
                o.Status,
                o.RecipientName,
                o.RecipientPhone,
                o.PackageId,
                o.Package.PackageName,
                o.OriginStationId,
                o.OriginStation.Name,
                o.DestinationStationId,
                o.DestinationStation.Name,
                o.DeliveryAddressId,
                DateTime.SpecifyKind(o.CreatedAt, DateTimeKind.Utc),
                DateTime.SpecifyKind(o.UpdatedAt, DateTimeKind.Utc)))
            .ToListAsync();

        return new PagedResult<DeliveryOrderSummaryDto>(items, page, pageSize, totalCount, totalPages);
    }

    public async Task<DeliveryOrderDetailDto> GetByIdAsync(Guid orderId, Guid customerId)
    {
        await EnsureActiveCustomerAsync(customerId);

        var order = await _db.DeliveryOrders
            .AsNoTracking()
            .Include(o => o.Package)
            .Include(o => o.OriginStation)
            .Include(o => o.DestinationStation)
            .Include(o => o.DeliveryAddress)
            .Include(o => o.StatusLogs)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Delivery order not found.");

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

    private static void ValidateFilterQuery(DeliveryOrderFilterQuery query)
    {
        var errors = new Dictionary<string, string[]>();

        if (query.Page < 1)
            errors["page"] = ["Page must be at least 1."];

        if (query.PageSize < 1 || query.PageSize > 100)
            errors["pageSize"] = ["PageSize must be between 1 and 100."];

        if (!string.IsNullOrWhiteSpace(query.Status) && !ValidOrderStatuses.Contains(query.Status.Trim()))
            errors["status"] = ["Invalid order status filter."];

        if (query.SearchKeyword is not null && query.SearchKeyword.Trim().Length > 50)
            errors["searchKeyword"] = ["Search keyword must be at most 50 characters."];

        if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate.Value > query.ToDate.Value)
            errors["fromDate"] = ["FromDate must be less than or equal to ToDate."];

        if (errors.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "One or more query parameters are invalid.", errors);
    }

    private static PackageDto ToPackageDto(Package p) => new(
        p.Id, p.SenderId, p.PackageName, p.WeightGram,
        p.LengthCm, p.WidthCm, p.HeightCm, p.PackageImageUrl, p.IsFragile, Utc(p.CreatedAt));

    private static CustomerAddressDto ToAddressDto(CustomerAddress a) => new(
        a.Id, a.UserId, a.Label, a.ContactName,
        a.ContactPhone, a.AddressLine, a.Latitude, a.Longitude, a.IsDefault, Utc(a.CreatedAt), Utc(a.UpdatedAt));

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
