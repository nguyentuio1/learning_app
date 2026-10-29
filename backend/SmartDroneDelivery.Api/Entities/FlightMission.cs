namespace SmartDroneDelivery.Api.Entities;

public class FlightMission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid DroneId { get; set; }
    public Guid? StartSlotId { get; set; }
    public Guid? DestinationSlotId { get; set; }
    public string FlightStatus { get; set; } = "PLANNED";
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public DeliveryOrder Order { get; set; } = null!;
    public Drone Drone { get; set; } = null!;
    public StationSlot? StartSlot { get; set; }
    public StationSlot? DestinationSlot { get; set; }
    public ICollection<FlightTelemetryLog> TelemetryLogs { get; set; } = new List<FlightTelemetryLog>();
}
