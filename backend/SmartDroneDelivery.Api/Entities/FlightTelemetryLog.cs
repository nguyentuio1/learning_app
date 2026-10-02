namespace SmartDroneDelivery.Api.Entities;

public class FlightTelemetryLog
{
    public long Id { get; set; }
    public Guid MissionId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal AltitudeMeters { get; set; }
    public decimal SpeedMps { get; set; }
    public int BatteryPercentage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public FlightMission Mission { get; set; } = null!;
}
