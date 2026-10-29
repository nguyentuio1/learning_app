namespace SmartDroneDelivery.Api.Entities;

public class AiChatMessage
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public string SenderType { get; set; } = "USER";
    public string MessageText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AiChatSession Session { get; set; } = null!;
}
