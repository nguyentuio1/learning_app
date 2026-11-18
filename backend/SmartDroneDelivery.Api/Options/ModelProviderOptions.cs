namespace SmartDroneDelivery.Api.Options;

public class ModelProviderOptions
{
    public const string SectionName = "ModelProvider";
    public string Provider { get; set; } = "Ollama";
    public string GeminiApiKey { get; set; } = "";
    public const string GeminiAssistantPolicyVersion = "LA-348-gemini-retention-v1";
    public string GeminiAssistantApprovedPolicyVersion { get; set; } = "";
    public bool HasValidConfiguration() => Provider == "Ollama"
        || Provider == "Gemini" && !string.IsNullOrWhiteSpace(GeminiApiKey);
}
