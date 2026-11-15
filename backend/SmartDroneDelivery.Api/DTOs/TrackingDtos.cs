namespace SmartDroneDelivery.Api.DTOs;

public record TrackingStation(Guid Id, string Name, decimal Latitude, decimal Longitude);

public record TrackingSnapshot(Guid OrderId, string TrackingNumber, string Status,
    TrackingStation OriginStation, TrackingStation DestinationStation, DateTime? EstimatedDeliveryTime,
    TelemetryPosition? LastPosition, bool TelemetryStale);

// Server-only correlation/version fields; never serialized as the public snapshot.
public record TrackingRead(TrackingSnapshot Snapshot, Guid? MissionId, DateTime UpdatedAt);
