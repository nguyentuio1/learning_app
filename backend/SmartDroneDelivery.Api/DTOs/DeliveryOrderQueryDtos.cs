using System.ComponentModel.DataAnnotations;

namespace SmartDroneDelivery.Api.DTOs;

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public record DeliveryOrderSummaryDto(
    Guid Id,
    string TrackingNumber,
    string Status,
    string RecipientName,
    string RecipientPhone,
    Guid PackageId,
    string PackageName,
    Guid OriginStationId,
    string OriginStationName,
    Guid DestinationStationId,
    string DestinationStationName,
    Guid? DeliveryAddressId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public class DeliveryOrderFilterQuery
{
    public string? Status { get; set; }
    [StringLength(50, ErrorMessage = "Search keyword must be at most 50 characters.")]
    public string? SearchKeyword { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")] public int Page { get; set; } = 1;
    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")] public int PageSize { get; set; } = 20;
}
