using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Health;

namespace SmartDroneDelivery.Tests;

/// <summary>LA-178: anonymous liveness and readiness endpoints that reveal no connection details.</summary>
public class HealthCheckTests
{
    private sealed class StubHandler(HttpStatusCode? status) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return status is { } code
                ? Task.FromResult(new HttpResponseMessage(code))
                : throw new HttpRequestException("Connection refused (minio:9000)");
        }
    }

    private static WebApplicationFactory<Program> WithStorage(AuthApiFactory factory, StubHandler storage) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHttpClient(HealthEndpoints.MinioHttpClient).ConfigurePrimaryHttpMessageHandler(() => storage)));

    private static async Task<(HttpStatusCode Status, JsonElement Body, string Raw)> GetAsync(WebApplicationFactory<Program> app, string path)
    {
        using var response = await app.CreateClient().GetAsync(path);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        return (response.StatusCode, JsonDocument.Parse(raw).RootElement, raw);
    }

    private static string CheckStatus(JsonElement body, string name) =>
        body.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name).GetProperty("status").GetString()!;

    [Fact]
    public async Task Liveness_is_anonymous_and_does_not_touch_dependencies()
    {
        var storage = new StubHandler(null);
        using var app = WithStorage(new AuthApiFactory(), storage);

        var (status, body, _) = await GetAsync(app, HealthEndpoints.LivePath);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Empty(body.GetProperty("checks").EnumerateArray());
        Assert.Empty(storage.Requests);
    }

    [Fact]
    public async Task Readiness_is_healthy_when_database_and_storage_answer()
    {
        var storage = new StubHandler(HttpStatusCode.OK);
        using var app = WithStorage(new AuthApiFactory(), storage);

        var (status, body, _) = await GetAsync(app, HealthEndpoints.ReadyPath);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal("Healthy", CheckStatus(body, "database"));
        Assert.Equal("Healthy", CheckStatus(body, "storage"));
        Assert.Equal("/minio/health/live", Assert.Single(storage.Requests).AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(null)]
    public async Task Readiness_returns_503_when_storage_is_down_without_leaking_details(HttpStatusCode? storageStatus)
    {
        using var app = WithStorage(new AuthApiFactory(), new StubHandler(storageStatus));

        var (status, body, raw) = await GetAsync(app, HealthEndpoints.ReadyPath);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal("Unhealthy", body.GetProperty("status").GetString());
        Assert.Equal("Unhealthy", CheckStatus(body, "storage"));
        Assert.Equal("Healthy", CheckStatus(body, "database"));
        Assert.DoesNotContain("minio", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refused", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Readiness_returns_503_when_the_database_is_unreachable()
    {
        using var app = WithStorage(new AuthApiFactory(), new StubHandler(HttpStatusCode.OK)).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<DbContextOptions>();
                // Nothing listens on port 1; the seed fails (and is logged), the health check must report it.
                services.AddDbContext<AppDbContext>(o => o.UseNpgsql("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2"));
            }));

        var (status, body, raw) = await GetAsync(app, HealthEndpoints.ReadyPath);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal("Unhealthy", CheckStatus(body, "database"));
        Assert.DoesNotContain("127.0.0.1", raw);
        Assert.DoesNotContain("Password", raw, StringComparison.OrdinalIgnoreCase);
    }
}
