using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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

public record StationSummaryDto(
    Guid Id,
    string StationCode,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Status);

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
    DateTime UpdatedAt,
    Guid? DeliveryAddressId = null);

public record DeliveryOrderDetailDto(
    Guid Id,
    string TrackingNumber,
    string Status,
    string? CancelReason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PickupSecureCode,
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
    IReadOnlyList<DeliveryStatusLogDto> StatusLogs,
    Guid? PackageId = null,
    string? PackageName = null,
    StationSummaryDto? OriginStation = null,
    StationSummaryDto? DestinationStation = null,
    object? AiEtaPrediction = null);

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
    [Required(ErrorMessage = "Package ID is required.")] Guid PackageId,
    [Required(ErrorMessage = "Origin station ID is required.")] Guid OriginStationId,
    [Required(ErrorMessage = "Destination station ID is required.")] Guid DestinationStationId,
    [Required(ErrorMessage = "Recipient name is required."), StringLength(150, MinimumLength = 1, ErrorMessage = "Recipient name must be 1 to 150 characters.")] string RecipientName,
    [Required(ErrorMessage = "Recipient phone is required."), RegularExpression(@"^\d{9,11}$", ErrorMessage = "Recipient phone must be 9 to 11 digits.")] string RecipientPhone,
    Guid? DeliveryAddressId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateDeliveryOrderRequest(
    [Required(ErrorMessage = "Recipient name is required."), StringLength(150, MinimumLength = 1, ErrorMessage = "Recipient name must be 1 to 150 characters.")] string? RecipientName,
    [Required(ErrorMessage = "Recipient phone is required."), RegularExpression(@"^\d{9,11}$", ErrorMessage = "Recipient phone must be 9 to 11 digits.")] string? RecipientPhone,
    Guid? DeliveryAddressId = null,
    [Required(ErrorMessage = "Destination station ID is required.")] Guid? DestinationStationId = null);

public record CancelDeliveryOrderRequest(
    [StringLength(500, ErrorMessage = "Cancellation reason must be at most 500 characters.")] string? Reason = null,
    [StringLength(500, ErrorMessage = "Cancellation reason must be at most 500 characters.")] string? CancellationReason = null)
{
    [JsonIgnore]
    public string? EffectiveReason => !string.IsNullOrWhiteSpace(CancellationReason) ? CancellationReason : Reason;
}

public class DeliveryOrderFilterQuery
{
    public string? Status { get; set; }
    public string? TrackingNumber { get; set; }
    public string? SearchKeyword { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")] public int Page { get; set; } = 1;
    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")] public int PageSize { get; set; } = 20;
}
