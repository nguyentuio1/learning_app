using System.ComponentModel.DataAnnotations;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.DTOs;

/// <summary>POST /api/delivery-orders (contract section 5.1). The client never sends customer, status, tracking number or code.</summary>
public record CreateDeliveryOrderRequest(
    Guid PackageId,
    Guid OriginStationId,
    Guid DestinationStationId,
    Guid? DeliveryAddressId,
    [Required][StringLength(150)] string RecipientName,
    [Required][RegularExpression(@"^\d{9,11}$", ErrorMessage = "Phone number must contain 9 to 11 digits.")] string RecipientPhone
);

public record StationSummaryDto(
    Guid Id, string StationCode, string Name, string Address, decimal Latitude, decimal Longitude, string Status)
{
    public static StationSummaryDto From(LandingStation s) =>
        new(s.Id, s.StationCode, s.Name, s.Address, s.Latitude, s.Longitude, s.Status);
}

public record StatusLogDto(string? FromStatus, string ToStatus, string? Remarks, DateTime CreatedAt);

/// <summary>Order detail (contract section 1). It never contains the pickup code.</summary>
public record DeliveryOrderDetailDto(
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
    PackageDto Package,
    StationSummaryDto OriginStation,
    StationSummaryDto DestinationStation,
    CustomerAddressDto? DeliveryAddress,
    string? CancelReason,
    DateTime? ScheduledDepartureTime,
    DateTime? ActualDepartureTime,
    DateTime? EstimatedDeliveryTime,
    DateTime? ActualDeliveryTime,
    object? AiEtaPrediction,
    IReadOnlyList<StatusLogDto> StatusLogs,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>The 201 response of POST /api/delivery-orders: the detail plus the pickup code, shown only this once.</summary>
public record CreatedDeliveryOrderDto : DeliveryOrderDetailDto
{
    public CreatedDeliveryOrderDto(DeliveryOrderDetailDto detail, string pickupSecureCode) : base(detail)
    {
        PickupSecureCode = pickupSecureCode;
    }

    public string PickupSecureCode { get; init; }
}
