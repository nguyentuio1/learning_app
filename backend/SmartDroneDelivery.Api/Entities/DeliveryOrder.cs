namespace SmartDroneDelivery.Api.Entities;

public class DeliveryOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TrackingNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid PackageId { get; set; }
    public Guid OriginStationId { get; set; }
    public Guid DestinationStationId { get; set; }
    public Guid? DispatcherId { get; set; }
    
    public string Status { get; set; } = "PENDING";
    public string? CancelReason { get; set; }
    
    public string PickupSecureCode { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    
    public DateTime? ScheduledDepartureTime { get; set; }
    public DateTime? ActualDepartureTime { get; set; }
    public DateTime? EstimatedDeliveryTime { get; set; }
    public DateTime? ActualDeliveryTime { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User Customer { get; set; } = null!;
    public Package Package { get; set; } = null!;
    public LandingStation OriginStation { get; set; } = null!;
    public LandingStation DestinationStation { get; set; } = null!;
    public User? Dispatcher { get; set; }
    public FlightMission? FlightMission { get; set; }
    public AiEtaPrediction? AiEtaPrediction { get; set; }
    public ICollection<DeliveryStatusLog> StatusLogs { get; set; } = new List<DeliveryStatusLog>();
}
