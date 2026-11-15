namespace SmartDroneDelivery.Api.DTOs;

public class TelemetrySampleRequest
{
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? AltitudeMeters { get; set; }
    public decimal? SpeedMps { get; set; }
    public int? BatteryPercentage { get; set; }
    // Keep the wire text so a timestamp without a timezone is rejected even on a UTC server.
    public string? Timestamp { get; set; }
}

public record TelemetryPosition(decimal Latitude, decimal Longitude, decimal AltitudeMeters,
    decimal SpeedMps, int BatteryPercentage, DateTime Timestamp);

// Internal read model for simulator/tracking services, not an additional HTTP response contract.
public enum TelemetryAvailability { NoSample, Fresh, Stale, NotInAir }

public record MissionTelemetrySnapshot(TelemetryAvailability Availability, DateTime? LastTelemetryAt,
    TelemetryPosition? LastPosition)
{
    public bool TelemetryStale => Availability is TelemetryAvailability.NoSample or TelemetryAvailability.Stale;
}
