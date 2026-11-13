using System.ComponentModel.DataAnnotations;

namespace SmartDroneDelivery.Api.DTOs;

public record ReportFailureRequest(string? FailureType, string? Reason);
public record ResolveFailureRequest(string? Action, string? Note, bool? PackageVerified);
public class FailedOrderFilterQuery
{
    public Guid? OriginStationId { get; set; }
    public Guid? DestinationStationId { get; set; }
    public string? SearchKeyword { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public string? FailureType { get; set; }
    public string? ResolutionStatus { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}
public record FailureEpisodeDto(long FailureLogId, string FailureType, string? FailureReason, DateTime FailedAt,
    string ResolutionStatus, string? LastResolutionAction, DateTime? ResolvedAt);
public record FailedOrderDto(Guid OrderId, string TrackingNumber, string Status, string PackageName,
    ReviewStationDto OriginStation, ReviewStationDto DestinationStation, long FailureLogId, string FailureType,
    string? FailureReason, DateTime FailedAt, string ResolutionStatus, string? LastResolutionAction,
    DateTime? ResolvedAt, DateTime UpdatedAt);
public record FailureHandlingEventDto(long FailureLogId, string Action, string? Note, Guid? PerformedBy, DateTime CreatedAt);
public record ParcelLocationDto(Guid StationId, Guid? SlotId);
public record FailureOrderDetailDto : DispatchOrderDetailDto
{
    public FailureOrderDetailDto(DispatchOrderDetailDto order, FailureEpisodeDto latestFailure, string returnToOriginStatus,
        ParcelLocationDto? parcelLocation, IReadOnlyList<FailureEpisodeDto> failureHistory,
        IReadOnlyList<FailureHandlingEventDto> resolutionHistory, bool resolutionHistoryComplete)
        : base(order.OrderId, order.TrackingNumber, order.Status, order.Package, order.OriginStation, order.DestinationStation,
            order.ScheduledDepartureTime, order.EstimatedDeliveryTime, order.ActualDepartureTime, order.ActualDeliveryTime,
            order.AssignmentStatus, order.CurrentMission, order.StatusLogs, order.CreatedAt, order.UpdatedAt)
    {
        LatestFailure = latestFailure; ReturnToOriginStatus = returnToOriginStatus; ParcelLocation = parcelLocation;
        FailureHistory = failureHistory; ResolutionHistory = resolutionHistory; ResolutionHistoryComplete = resolutionHistoryComplete;
    }
    public FailureEpisodeDto LatestFailure { get; init; }
    public string ReturnToOriginStatus { get; init; }
    public ParcelLocationDto? ParcelLocation { get; init; }
    public IReadOnlyList<FailureEpisodeDto> FailureHistory { get; init; }
    public IReadOnlyList<FailureHandlingEventDto> ResolutionHistory { get; init; }
    public bool ResolutionHistoryComplete { get; init; }
}
public record ActiveDeliveryDto(Guid OrderId, string TrackingNumber, string OrderStatus, Guid MissionId, string FlightStatus,
    string DroneSerialNumber, Guid OriginStationId, Guid DestinationStationId, DateTime? ScheduledDepartureTime,
    DateTime? EstimatedDeliveryTime, DateTime? LastTelemetryAt, bool TelemetryStale);
