namespace SmartDroneDelivery.Api.Entities;

public class Drone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SerialNumber { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public decimal MaxPayloadGram { get; set; }
    public decimal MaxFlightRangeKm { get; set; }
    public int BatteryCapacityMah { get; set; }
    public string Status { get; set; } = "IDLE";
    public Guid? CurrentStationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public LandingStation? CurrentStation { get; set; }
    public ICollection<FlightMission> FlightMissions { get; set; } = new List<FlightMission>();
}
