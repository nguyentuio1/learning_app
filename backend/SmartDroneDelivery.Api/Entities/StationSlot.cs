namespace SmartDroneDelivery.Api.Entities;

public class StationSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StationId { get; set; }
    public int SlotNumber { get; set; }
    public string Status { get; set; } = "EMPTY";
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public LandingStation Station { get; set; } = null!;
    public ICollection<FlightMission> DepartureMissions { get; set; } = new List<FlightMission>();
    public ICollection<FlightMission> DestinationMissions { get; set; } = new List<FlightMission>();
}
