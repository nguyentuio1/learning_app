namespace SmartDroneDelivery.Api.Entities;

/// <summary>Database-serialized monthly allocation; reserved values may have gaps.</summary>
public class DeliveryTrackingCounter
{
    public string Month { get; set; } = string.Empty;
    public long LastValue { get; set; }
}
