using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Api.Health;

/// <summary>
/// LA-178: <c>GET /health/live</c> says the process answers; <c>GET /health/ready</c> also checks PostgreSQL and MinIO.
/// Both are anonymous (Docker, load balancers) and only report status names and durations: no exception text,
/// host names or connection details.
/// </summary>
public static class HealthEndpoints
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";
    public const string ReadyTag = "ready";
    public const string MinioHttpClient = "minio-health";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddSystemHealthChecks(this IServiceCollection services)
    {
        services.AddHttpClient(MinioHttpClient, client => client.Timeout = TimeSpan.FromSeconds(3));
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag])
            .AddCheck<MinioHealthCheck>("storage", tags: [ReadyTag]);
        return services;
    }

    public static IEndpointRouteBuilder MapSystemHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteAsync })
            .AllowAnonymous();
        endpoints.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = c => c.Tags.Contains(ReadyTag), ResponseWriter = WriteAsync })
            .AllowAnonymous();
        return endpoints;
    }

    private static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = "no-store";
        var body = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds),
            }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}

public sealed class DatabaseHealthCheck(AppDbContext db, ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Database health check failed.");
            return HealthCheckResult.Unhealthy();
        }
    }
}

/// <summary>Calls MinIO's unauthenticated liveness endpoint (<c>/minio/health/live</c>).</summary>
public sealed class MinioHealthCheck(IHttpClientFactory clients, IOptions<MinioOptions> options, ILogger<MinioHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var minio = options.Value;
        var url = $"{(minio.WithSSL ? "https" : "http")}://{minio.Endpoint}/minio/health/live";
        try
        {
            using var response = await clients.CreateClient(HealthEndpoints.MinioHttpClient).GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode) return HealthCheckResult.Healthy();
            logger.LogWarning("Storage health check returned {StatusCode}.", (int)response.StatusCode);
            return HealthCheckResult.Unhealthy();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            logger.LogWarning(ex, "Storage health check failed.");
            return HealthCheckResult.Unhealthy();
        }
    }
}
