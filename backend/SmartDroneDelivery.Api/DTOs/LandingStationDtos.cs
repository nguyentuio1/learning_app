using System.Text.Json.Serialization;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.DTOs;

public record LandingStationDto(
    Guid Id,
    string StationCode,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Status,
    int TotalSlots,
    int EmptySlots,
    Guid? OperatorId,
    string? OperatorName,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static LandingStationDto From(LandingStation s) => new(
        s.Id,
        s.StationCode,
        s.Name,
        s.Address,
        s.Latitude,
        s.Longitude,
        s.Status,
        s.TotalSlots,
        s.Slots?.Count(slot => slot.Status == "EMPTY") ?? 0,
        s.OperatorId,
        s.Operator?.FullName,
        DateTime.SpecifyKind(s.CreatedAt, DateTimeKind.Utc),
        DateTime.SpecifyKind(s.UpdatedAt, DateTimeKind.Utc));
}

public record LandingStationSlotDto(
    Guid Id,
    Guid StationId,
    int SlotNumber,
    string Status,
    DateTime LastUpdated,
    Guid? CurrentOrderId,
    Guid? CurrentMissionId);

public record LandingStationDetailDto(
    Guid Id,
    string StationCode,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Status,
    int TotalSlots,
    int EmptySlots,
    Guid? OperatorId,
    string? OperatorName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<LandingStationSlotDto> Slots,
    int HostedDroneCount);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateLandingStationRequest
{
    public string? StationCode { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? TotalSlots { get; set; }
    public Guid? OperatorId { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class UpdateLandingStationRequest
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public Guid? OperatorId { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ChangeLandingStationStatusRequest
{
    public string? NewStatus { get; set; }
    public string? Reason { get; set; }
}

public record LandingStationStatusChangeResponseDto(
    LandingStationDto Station,
    bool RerouteWarning,
    IReadOnlyList<Guid> AffectedMissionIds);
