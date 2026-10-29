namespace SmartDroneDelivery.Api.Entities;

public class Package
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public decimal WeightGram { get; set; }
    public decimal LengthCm { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }
    public string? PackageImageUrl { get; set; }
    public bool IsFragile { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Sender { get; set; } = null!;
    public DeliveryOrder? DeliveryOrder { get; set; }
}
