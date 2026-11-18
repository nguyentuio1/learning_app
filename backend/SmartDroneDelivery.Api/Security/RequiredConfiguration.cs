namespace SmartDroneDelivery.Api.Security;

/// <summary>
/// LA-328: the repository holds no working secret. The API refuses to start when a secret is missing, and outside
/// Development it also refuses the values that were committed before LA-328, because they stay readable in the git history.
/// Error messages name the setting, never its value.
/// </summary>
public static class RequiredConfiguration
{
    public const string ConnectionStringKey = "ConnectionStrings:DefaultConnection";
    public const string JwtKey = "Jwt:Key";
    public const string MinioAccessKey = "Minio:AccessKey";
    public const string MinioSecretKey = "Minio:SecretKey";
    public const string AdminEmailKey = "Seed:AdminEmail";
    public const string AdminPasswordKey = "Seed:AdminPassword";

    public const string DefaultAdminEmail = "admin@smartdronedelivery.com";

    // Committed before LA-328 (appsettings.json, docker-compose.yml, SeedData.cs). Treat them as public.
    private const string RetiredJwtKey = "SmartDroneDelivery_Super_Secret_Key_For_Jwt_Token_2026_Secure_Auth";
    private const string RetiredDatabasePassword = "Password=Password123!";
    private const string RetiredMinioSecret = "minioadminpassword";
    private const string RetiredAdminPassword = "Admin@123456";

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        var problems = new List<string>();

        foreach (var key in new[] { ConnectionStringKey, JwtKey, MinioAccessKey, MinioSecretKey })
        {
            if (string.IsNullOrWhiteSpace(configuration[key]))
                problems.Add($"{key} is not set (environment variable {ToEnvironmentVariable(key)})");
        }

        var jwtKey = configuration[JwtKey];
        if (!string.IsNullOrWhiteSpace(jwtKey) && System.Text.Encoding.UTF8.GetByteCount(jwtKey) < 32)
            problems.Add($"{JwtKey} must be at least 32 bytes long");

        var adminPassword = configuration[AdminPasswordKey];
        if (!string.IsNullOrEmpty(adminPassword) && !IsStrongEnough(adminPassword))
            problems.Add($"{AdminPasswordKey} must be 8 to 100 characters with at least one letter and one digit");

        if (!environment.IsDevelopment())
        {
            if (jwtKey == RetiredJwtKey) problems.Add(Retired(JwtKey));
            if (configuration[ConnectionStringKey]?.Contains(RetiredDatabasePassword, StringComparison.OrdinalIgnoreCase) == true)
                problems.Add(Retired(ConnectionStringKey));
            if (configuration[MinioSecretKey] == RetiredMinioSecret) problems.Add(Retired(MinioSecretKey));
            if (adminPassword == RetiredAdminPassword) problems.Add(Retired(AdminPasswordKey));
        }

        if (problems.Count > 0)
            throw new InvalidOperationException(
                "The API cannot start because of missing or unsafe configuration: " + string.Join("; ", problems) +
                ". Set the values as environment variables or with dotnet user-secrets (README, \"Configuration and secrets\").");
    }

    /// <summary>The same rule as registration (<c>RegisterRequest</c>).</summary>
    public static bool IsStrongEnough(string password) =>
        password.Length is >= 8 and <= 100 && password.Any(char.IsAsciiLetter) && password.Any(char.IsAsciiDigit);

    public static string ToEnvironmentVariable(string key) => key.Replace(":", "__");

    private static string Retired(string key) =>
        $"{key} uses a value that was committed to the repository before LA-328; rotate it";
}
