namespace SmartDroneDelivery.Api.Entities;

public class DeliveryStatusLog
{
    public long Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid? ChangedBy { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DeliveryOrder Order { get; set; } = null!;
    public User? ChangedByUser { get; set; }
}
