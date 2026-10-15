namespace SmartDroneDelivery.Api.Options;

public class MinioOptions
{
    public const string SectionName = "Minio";

    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public bool WithSSL { get; set; }

    /// <summary>Bucket that holds the avatars. Objects are readable without a login so the apps can show them.</summary>
    public string AvatarBucket { get; set; } = "avatars";

    /// <summary>Base URL of MinIO as the clients see it. Empty means built from <see cref="Endpoint"/>.</summary>
    public string? PublicBaseUrl { get; set; }
}
