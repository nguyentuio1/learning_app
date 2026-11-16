namespace SmartDroneDelivery.Api.Options;

public class DeliverySummaryOptions
{
    public const string SectionName = "DeliverySummary";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://127.0.0.1:11434";
    public string ModelName { get; set; } = "";
    public string ModelRevision { get; set; } = "";
    public bool HasValidConfiguration() => !Enabled || (!string.IsNullOrWhiteSpace(ModelName) && ModelName.Length <= 200
        && !string.IsNullOrWhiteSpace(ModelRevision) && ModelRevision.Length <= 100 && TryGetEndpoint(out _));
    public bool TryGetEndpoint(out Uri? endpoint)
    {
        endpoint = null;
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var origin) || !origin.IsLoopback
            || origin.Scheme is not ("http" or "https") || origin.UserInfo.Length != 0
            || origin.Query.Length != 0 || origin.Fragment.Length != 0) return false;
        endpoint = new Uri(origin, "/api/generate");
        return true;
    }
}
