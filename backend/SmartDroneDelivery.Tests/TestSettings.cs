using Microsoft.AspNetCore.Hosting;

namespace SmartDroneDelivery.Tests;

/// <summary>
/// LA-328: the API reads its secrets from configuration only. The test hosts supply these throw-away values;
/// they are not used by any real environment.
/// </summary>
public static class TestSettings
{
    public const string AdminEmail = "admin@smartdronedelivery.com";
    public const string AdminPassword = "TestOnly-Admin-2026";

    /// <summary>
    /// <c>UseSetting</c>, not <c>ConfigureAppConfiguration</c>: Program reads the secrets before <c>Build()</c>,
    /// and only host settings are visible to it at that point.
    /// </summary>
    public static void Apply(IWebHostBuilder builder)
    {
        foreach (var (key, value) in Values()) builder.UseSetting(key, value);
    }

    public static Dictionary<string, string?> Values() => new()
    {
        ["Seed:DevelopmentData"] = "false",
        ["Seed:AdminEmail"] = AdminEmail,
        ["Seed:AdminPassword"] = AdminPassword,
        ["Jwt:Key"] = "test-only-signing-key-that-is-at-least-32-bytes-long",
        // The test hosts replace the DbContext and the MinIO storage; these values only satisfy the startup check.
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused-by-tests",
        ["Minio:AccessKey"] = "test-access-key",
        ["Minio:SecretKey"] = "test-secret-key",
    };
}
