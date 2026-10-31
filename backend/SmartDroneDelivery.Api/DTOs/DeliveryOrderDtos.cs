using System.ComponentModel.DataAnnotations;

namespace SmartDroneDelivery.Api.DTOs;

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public record DeliveryOrderSummaryDto(
    Guid Id,
    string TrackingNumber,
    string Status,
    Guid CustomerId,
    string CustomerName,
    Guid PackageId,
    string PackageName,
    decimal WeightGram,
    Guid OriginStationId,
    string OriginStationName,
    Guid DestinationStationId,
    string DestinationStationName,
    string RecipientName,
    string RecipientPhone,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record DeliveryOrderDetailDto(
    Guid Id,
    string TrackingNumber,
    string Status,
    string? CancelReason,
    string PickupSecureCode,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    PackageDto Package,
    Guid OriginStationId,
    string OriginStationName,
    string OriginStationAddress,
    Guid DestinationStationId,
    string DestinationStationName,
    string DestinationStationAddress,
    Guid? DeliveryAddressId,
    CustomerAddressDto? DeliveryAddress,
    Guid? DispatcherId,
    string? DispatcherName,
    string RecipientName,
    string RecipientPhone,
    DateTime? ScheduledDepartureTime,
    DateTime? ActualDepartureTime,
    DateTime? EstimatedDeliveryTime,
    DateTime? ActualDeliveryTime,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<DeliveryStatusLogDto> StatusLogs);

public record DeliveryStatusLogDto(
    long Id,
    Guid OrderId,
    string? FromStatus,
    string ToStatus,
    string? Remarks,
    Guid? ChangedBy,
    string? ChangedByName,
    DateTime CreatedAt);

public record CreateDeliveryOrderRequest(
    [Required] Guid PackageId,
    [Required] Guid OriginStationId,
    [Required] Guid DestinationStationId,
    [Required, StringLength(150, MinimumLength = 1)] string RecipientName,
    [Required, Phone, StringLength(20)] string RecipientPhone,
    Guid? DeliveryAddressId);

public record UpdateDeliveryOrderRequest(
    [StringLength(150, MinimumLength = 1)] string? RecipientName,
    [Phone, StringLength(20)] string? RecipientPhone,
    Guid? DeliveryAddressId);

public record CancelDeliveryOrderRequest(
    [Required, StringLength(500, MinimumLength = 3)] string Reason);

public class DeliveryOrderFilterQuery
{
    public string? Status { get; set; }
    public string? TrackingNumber { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}
