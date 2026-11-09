using System.Text.Json.Serialization;

namespace SmartDroneDelivery.Api.DTOs;

public record StationOrderInfoDto(
    Guid OrderId,
    string TrackingNumber,
    string Status,
    string Role,
    string PackageName,
    decimal DeclaredWeightGram,
    decimal DeclaredLengthCm,
    decimal DeclaredWidthCm,
    decimal DeclaredHeightCm,
    bool IsFragile,
    Guid OriginStationId,
    Guid DestinationStationId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class PackageIntakeRequest
{
    public string? TrackingNumber { get; set; }
    public Guid? SlotId { get; set; }
    public decimal? MeasuredWeightGram { get; set; }
    public decimal? MeasuredLengthCm { get; set; }
    public decimal? MeasuredWidthCm { get; set; }
    public decimal? MeasuredHeightCm { get; set; }
    public bool? VisualInspectionPassed { get; set; }
}

public record PackageIntakeResponseDto(
    Guid OrderId,
    string TrackingNumber,
    string Status,
    Guid SlotId,
    int SlotNumber,
    DateTime ConfirmedAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class RejectIntakeRequest
{
    public string? TrackingNumber { get; set; }
    public string? Reason { get; set; }
}

public record RejectIntakeResponseDto(
    Guid OrderId,
    string TrackingNumber,
    string Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ConfirmPickupRequest
{
    public string? TrackingNumber { get; set; }
    public string? PickupSecureCode { get; set; }
}

public record ConfirmPickupResponseDto(
    Guid OrderId,
    string TrackingNumber,
    string Status,
    DateTime ActualDeliveryTime,
    Guid SlotId,
    bool SlotReleased);

public record ValidateSecureCodeResponseDto(
    bool IsValid,
    int AttemptsRemaining,
    bool IsLocked,
    DateTime? LockoutExpiresAt);
