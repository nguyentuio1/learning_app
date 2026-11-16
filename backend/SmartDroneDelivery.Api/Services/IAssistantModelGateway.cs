namespace SmartDroneDelivery.Api.Services;

public enum AssistantIntent { Status, Eta, History, Help, Clarify, Refuse }
public record AssistantContextMessage(string SenderType, string MessageText);
public record AssistantModelInput(string Message, AssistantContextMessage[] History);
public interface IAssistantModelGateway
{
    bool IsConfigured { get; }
    Task<AssistantIntent> ClassifyAsync(AssistantModelInput input, CancellationToken ct);
}
