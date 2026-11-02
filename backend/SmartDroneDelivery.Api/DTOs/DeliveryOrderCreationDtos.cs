using System.ComponentModel.DataAnnotations;

namespace SmartDroneDelivery.Api.DTOs;

public record StationSummaryDto(
    Guid Id,
    string StationCode,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Status);

public record CreatedOrderStatusLogDto(
    long Id,
    Guid OrderId,
    string? FromStatus,
    string ToStatus,
    string? Remarks,
    Guid? ChangedBy,
    DateTime CreatedAt);

public record CreatedDeliveryOrderDto(
    Guid Id,
    string TrackingNumber,
    string Status,
    string PickupSecureCode,
    Guid CustomerId,
    Guid PackageId,
    Guid OriginStationId,
    Guid DestinationStationId,
    Guid? DeliveryAddressId,
    string RecipientName,
    string RecipientPhone,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CreatedOrderStatusLogDto> StatusLogs);

public record CreateDeliveryOrderRequest(
    [Required(ErrorMessage = "Package ID is required.")] Guid PackageId,
    [Required(ErrorMessage = "Origin station ID is required.")] Guid OriginStationId,
    [Required(ErrorMessage = "Destination station ID is required.")] Guid DestinationStationId,
    [Required(ErrorMessage = "Recipient name is required."), StringLength(150, MinimumLength = 1, ErrorMessage = "Recipient name must be 1 to 150 characters.")] string RecipientName,
    [Required(ErrorMessage = "Recipient phone is required."), RegularExpression(@"^\d{9,11}$", ErrorMessage = "Recipient phone must be 9 to 11 digits.")] string RecipientPhone,
    Guid? DeliveryAddressId);
