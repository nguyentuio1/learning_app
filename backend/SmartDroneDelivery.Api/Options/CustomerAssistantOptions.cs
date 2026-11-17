namespace SmartDroneDelivery.Api.Options;

public class CustomerAssistantOptions
{
    public const string SectionName = "CustomerAssistant";
    public const string PolicyVersion = "LA-165-retention-v1";
    public bool Enabled { get; set; }
    public string ApprovedPolicyVersion { get; set; } = "";
    public int RetentionDays { get; set; } = 30;
    public int BackupRetentionDays { get; set; }
    public string BaseUrl { get; set; } = "http://127.0.0.1:11434";
    public string ModelName { get; set; } = "";
    public string ModelRevision { get; set; } = "";
    public bool HasApprovedRetention => ApprovedPolicyVersion == PolicyVersion && RetentionDays == 30 && BackupRetentionDays is >= 1 and <= 30;
    public bool HasValidConfiguration() => (string.IsNullOrEmpty(ApprovedPolicyVersion) || HasApprovedRetention)
        && (!Enabled || HasApprovedRetention && !string.IsNullOrWhiteSpace(ModelName) && ModelName.Length <= 200
            && !string.IsNullOrWhiteSpace(ModelRevision) && ModelRevision.Length <= 100 && TryGetEndpoint(out _));
    public bool TryGetEndpoint(out Uri? endpoint)
    {
        endpoint = null;
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var origin) || !origin.IsLoopback
            || origin.Scheme is not ("http" or "https") || origin.UserInfo.Length != 0
            || origin.Query.Length != 0 || origin.Fragment.Length != 0) return false;
        endpoint = new Uri(origin, "/api/generate"); return true;
    }
}
