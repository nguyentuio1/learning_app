using System.ComponentModel.DataAnnotations;

namespace SmartDroneDelivery.Api.DTOs;

public record ScheduleOrderRequest(string? ScheduledDepartureTime);
public record AssignDroneRequest(Guid? DroneId);
public record ScheduleDto(Guid OrderId, DateTime ScheduledDepartureTime, DateTime EstimatedDeliveryTime, int EstimatedFlightMinutes);
public record FlightMissionDto(Guid Id, Guid OrderId, Guid DroneId, string FlightStatus, Guid DestinationSlotId, DateTime ScheduledDepartureTime);
public record DroneCandidateDto(Guid DroneId, string SerialNumber, string ModelName, decimal MaxPayloadGram,
    decimal MaxFlightRangeKm, Guid? CurrentStationId, string Status, bool IsEligible, IReadOnlyList<string> ReasonsNotEligible);

public class ApprovedOrderFilterQuery
{
    public Guid? OriginStationId { get; set; }
    public Guid? DestinationStationId { get; set; }
    public string? SearchKeyword { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public string? ScheduledFrom { get; set; }
    public string? ScheduledTo { get; set; }
    public string? AssignmentStatus { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public record ApprovedOrderDto(Guid OrderId, string TrackingNumber, string Status, Guid PackageId, string PackageName,
    decimal WeightGram, ReviewStationDto OriginStation, ReviewStationDto DestinationStation, DateTime? ScheduledDepartureTime,
    DateTime? EstimatedDeliveryTime, string AssignmentStatus, Guid? MissionId, Guid? DroneId, string? DroneSerialNumber,
    DateTime CreatedAt, DateTime UpdatedAt);
public record DispatchDroneDto(Guid Id, string SerialNumber, string ModelName, string Status);
public record DispatchMissionDto(Guid Id, string FlightStatus, DispatchDroneDto Drone, Guid? StartSlotId,
    Guid? DestinationSlotId, DateTime? StartTime, DateTime? EndTime);
public record DispatchOrderDetailDto(Guid OrderId, string TrackingNumber, string Status, PackageDto Package,
    StationSummaryDto OriginStation, StationSummaryDto DestinationStation, DateTime? ScheduledDepartureTime,
    DateTime? EstimatedDeliveryTime, DateTime? ActualDepartureTime, DateTime? ActualDeliveryTime, string AssignmentStatus,
    DispatchMissionDto? CurrentMission, IReadOnlyList<StatusLogDto> StatusLogs, DateTime CreatedAt, DateTime UpdatedAt);
