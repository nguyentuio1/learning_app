namespace SmartDroneDelivery.Api.Entities;

/// <summary>Immutable classification and source of one status-log entry into FAILED.</summary>
public class DeliveryFailureEpisode
{
    public long FailureLogId { get; set; }
    public string FailureType { get; set; } = "UNKNOWN";
    public string Source { get; set; } = string.Empty;
    public string? OriginalReason { get; set; }
    public DeliveryStatusLog FailureLog { get; set; } = null!;
}

public class FailureHandlingEvent
{
    public long Id { get; set; }
    public long FailureLogId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Note { get; set; }
    public Guid? PerformedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    // Events may also refer to a legacy FAILED log without inventing its missing episode metadata.
    public DeliveryStatusLog FailureLog { get; set; } = null!;
    public User? Actor { get; set; }
}
