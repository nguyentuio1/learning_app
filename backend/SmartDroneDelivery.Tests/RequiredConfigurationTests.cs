using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Tests;

/// <summary>LA-328: no working secret in the repository; the API fails fast when one is missing or was committed.</summary>
public class RequiredConfigurationTests
{
    private const string GoodJwtKey = "a-fresh-signing-key-of-more-than-32-bytes";

    private static IConfiguration Config(Dictionary<string, string?> overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=db;Database=app;Username=app;Password=fresh-db-password",
            ["Jwt:Key"] = GoodJwtKey,
            ["Minio:AccessKey"] = "fresh-access",
            ["Minio:SecretKey"] = "fresh-minio-secret",
            ["Seed:AdminPassword"] = "Fresh-Admin-2026",
        };
        foreach (var (key, value) in overrides) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static InvalidOperationException ValidateFails(Dictionary<string, string?> overrides, string environment = "Production") =>
        Assert.Throws<InvalidOperationException>(() => RequiredConfiguration.Validate(Config(overrides), new Env(environment)));

    [Fact]
    public void Accepts_complete_fresh_configuration_in_production()
    {
        RequiredConfiguration.Validate(Config(new()), new Env("Production"));
    }

    [Theory]
    [InlineData("ConnectionStrings:DefaultConnection", "ConnectionStrings__DefaultConnection")]
    [InlineData("Jwt:Key", "Jwt__Key")]
    [InlineData("Minio:AccessKey", "Minio__AccessKey")]
    [InlineData("Minio:SecretKey", "Minio__SecretKey")]
    public void Missing_secret_fails_in_every_environment_and_names_the_variable(string key, string variable)
    {
        foreach (var environment in new[] { "Production", "Development" })
        {
            var error = ValidateFails(new() { [key] = "" }, environment);
            Assert.Contains(key, error.Message);
            Assert.Contains(variable, error.Message);
        }
    }

    [Fact]
    public void Short_jwt_key_fails()
    {
        var error = ValidateFails(new() { ["Jwt:Key"] = "too-short" });
        Assert.Contains("at least 32 bytes", error.Message);
    }

    [Fact]
    public void Weak_admin_password_fails_without_echoing_it()
    {
        var error = ValidateFails(new() { ["Seed:AdminPassword"] = "onlyletters" });
        Assert.Contains("Seed:AdminPassword", error.Message);
        Assert.DoesNotContain("onlyletters", error.Message);
    }

    [Theory]
    [InlineData("Jwt:Key", "SmartDroneDelivery_Super_Secret_Key_For_Jwt_Token_2026_Secure_Auth")]
    [InlineData("ConnectionStrings:DefaultConnection", "Host=localhost;Port=5433;Database=drone_delivery_db;Username=postgres;Password=Password123!")]
    [InlineData("Minio:SecretKey", "minioadminpassword")]
    [InlineData("Seed:AdminPassword", "Admin@123456")]
    public void Values_committed_before_LA_328_are_refused_outside_development(string key, string retired)
    {
        var error = ValidateFails(new() { [key] = retired });
        Assert.Contains(key, error.Message);
        Assert.Contains("rotate", error.Message);
        Assert.DoesNotContain(retired, error.Message);

        // Local development may still point at an old local container.
        RequiredConfiguration.Validate(Config(new() { [key] = retired }), new Env("Development"));
    }

    [Fact]
    public void Committed_settings_files_hold_no_secret_values()
    {
        var apiDirectory = Path.GetDirectoryName(typeof(RequiredConfiguration).Assembly.Location)!;
        var committed = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(apiDirectory, "appsettings.json"))
            .AddJsonFile(Path.Combine(apiDirectory, "appsettings.Development.json"), optional: true)
            .Build();

        foreach (var key in new[] { "ConnectionStrings:DefaultConnection", "Jwt:Key", "Minio:AccessKey", "Minio:SecretKey", "Seed:AdminPassword" })
            Assert.True(string.IsNullOrEmpty(committed[key]), $"{key} must not be committed");
    }

    [Fact]
    public void Api_host_refuses_to_start_without_a_jwt_key()
    {
        using var factory = new AuthApiFactory().WithWebHostBuilder(builder => builder.UseSetting("Jwt:Key", ""));

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Jwt:Key", error.Message);
    }

    [Fact]
    public async Task Administrator_is_created_from_configuration()
    {
        using var factory = new AuthApiFactory();
        factory.CreateClient();

        await factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.Include(u => u.Role).SingleAsync(u => u.Email == TestSettings.AdminEmail);
            Assert.Equal("ADMIN", admin.Role.Code);
            Assert.True(BCrypt.Net.BCrypt.Verify(TestSettings.AdminPassword, admin.PasswordHash));
            Assert.False(BCrypt.Net.BCrypt.Verify("Admin@123456", admin.PasswordHash));
        });
    }

    [Fact]
    public async Task No_administrator_is_created_without_a_configured_password()
    {
        using var factory = new AuthApiFactory().WithWebHostBuilder(builder => builder.UseSetting("Seed:AdminPassword", ""));
        factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync());
        Assert.Equal(5, await db.Roles.CountAsync());
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "SmartDroneDelivery.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
