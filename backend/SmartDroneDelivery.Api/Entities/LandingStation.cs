namespace SmartDroneDelivery.Api.Entities;

public class LandingStation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StationCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string Address { get; set; } = string.Empty;
    public int TotalSlots { get; set; } = 2;
    public string Status { get; set; } = "OPERATIONAL";
    public Guid? OperatorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? Operator { get; set; }
    public ICollection<StationSlot> Slots { get; set; } = new List<StationSlot>();
    public ICollection<Drone> StationDrones { get; set; } = new List<Drone>();
    public ICollection<DeliveryOrder> OriginOrders { get; set; } = new List<DeliveryOrder>();
    public ICollection<DeliveryOrder> DestinationOrders { get; set; } = new List<DeliveryOrder>();
}
