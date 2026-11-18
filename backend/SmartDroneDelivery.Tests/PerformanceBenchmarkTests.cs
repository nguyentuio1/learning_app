// LA-176: Automated performance and concurrency benchmark against ASP.NET Core TestServer
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;
using Xunit.Abstractions;

namespace SmartDroneDelivery.Tests;

/// <summary>
/// Gates the in-process load benchmark behind <see cref="EnableVariable"/> so default CI runs
/// (2-vCPU runners where concurrent BCrypt login hashing exceeds 3,000 ms) skip it cleanly.
/// </summary>
public sealed class PerformanceFactAttribute : FactAttribute
{
    public const string EnableVariable = "RUN_PERFORMANCE_BENCHMARKS";

    public PerformanceFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnableVariable), "true", StringComparison.OrdinalIgnoreCase))
        {
            Skip = $"Set {EnableVariable}=true to run in-process load benchmarks.";
        }
    }
}

[Trait("Category", "Performance")]
public class PerformanceBenchmarkTests(AuthApiFactory factory, ITestOutputHelper output) : IClassFixture<AuthApiFactory>
{
    private const string Password = "Passw0rd123";

    [PerformanceFact]
    public async Task Concurrent_load_benchmark_10_50_100_meets_3000ms_p95_target()
    {
        using var setupClient = factory.CreateClient();
        var n = Random.Shared.Next(10000, 99999);
        var email = $"perf-{n}@example.com";

        var reg = await setupClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = Password,
            fullName = "Perf User",
            phoneNumber = $"091{n:D7}"[..10]
        });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        var auth = await reg.Content.ReadFromJsonAsync<JsonElement>();
        var customerId = Guid.Parse(auth.GetProperty("user").GetProperty("id").GetString()!);
        var token = auth.GetProperty("accessToken").GetString()!;

        Guid originId = Guid.Empty, destId = Guid.Empty, orderId = Guid.Empty;
        var packageIds = new List<Guid>(160);
        await factory.WithDbAsync(async db =>
        {
            var origin = new LandingStation { StationCode = $"PF-O-{n}", Name = "Perf Origin", Address = "A", Latitude = 10.7m, Longitude = 106.6m, Status = "OPERATIONAL" };
            var dest = new LandingStation { StationCode = $"PF-D-{n}", Name = "Perf Dest", Address = "B", Latitude = 10.8m, Longitude = 106.7m, Status = "OPERATIONAL" };
            var pkgs = Enumerable.Range(0, 161).Select(i => new Package
            {
                SenderId = customerId,
                PackageName = $"Perf Box {i}",
                WeightGram = 500,
                LengthCm = 20,
                WidthCm = 15,
                HeightCm = 10
            }).ToList();
            var order = new DeliveryOrder
            {
                CustomerId = customerId,
                Package = pkgs[0],
                OriginStation = origin,
                DestinationStation = dest,
                TrackingNumber = $"PF-TRK-{n}",
                RecipientName = "Perf Recipient",
                RecipientPhone = "0900000000",
                PickupSecureCode = "123456",
                Status = "IN_TRANSIT"
            };
            db.LandingStations.AddRange(origin, dest);
            db.Packages.AddRange(pkgs);
            db.DeliveryOrders.Add(order);
            await db.SaveChangesAsync();
            originId = origin.Id;
            destId = dest.Id;
            packageIds.AddRange(pkgs.Skip(1).Select(p => p.Id));
            orderId = order.Id;
        });

        // Warm-up
        await MeasureAsync("Warmup GET /health/live", 5, () => setupClient.GetAsync("/health/live"));

        var pkgCursor = -1;
        var violations = new List<string>();
        foreach (var concurrency in new[] { 10, 50, 100 })
        {
            await AssertTierAsync(violations, "GET /health/live", concurrency, async () =>
            {
                using var c = factory.CreateClient();
                return await c.GetAsync("/health/live");
            });

            await AssertTierAsync(violations, "POST /api/auth/login", concurrency, async () =>
            {
                using var c = factory.CreateClient();
                return await c.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
            });

            await AssertTierAsync(violations, "GET /api/delivery-orders", concurrency, async () =>
            {
                using var c = factory.CreateClient();
                c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return await c.GetAsync("/api/delivery-orders?page=1&pageSize=20");
            });

            await AssertTierAsync(violations, "GET /api/delivery-orders/{id}/tracking", concurrency, async () =>
            {
                using var c = factory.CreateClient();
                c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return await c.GetAsync($"/api/delivery-orders/{orderId}/tracking");
            });

            await AssertTierAsync(violations, "POST /api/delivery-orders", concurrency, async () =>
            {
                var idx = Interlocked.Increment(ref pkgCursor);
                using var c = factory.CreateClient();
                c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return await c.PostAsJsonAsync("/api/delivery-orders", new
                {
                    packageId = packageIds[idx],
                    originStationId = originId,
                    destinationStationId = destId,
                    recipientName = "Concurrent Recipient",
                    recipientPhone = "0901234567"
                });
            }, expectedCode: HttpStatusCode.Created);
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private async Task AssertTierAsync(List<string> violations, string endpoint, int concurrency, Func<Task<HttpResponseMessage>> call, HttpStatusCode expectedCode = HttpStatusCode.OK)
    {
        var (latenciesMs, totalMs, failures) = await MeasureAsync(endpoint, concurrency, call, expectedCode);
        var sorted = latenciesMs.OrderBy(x => x).ToArray();
        var min = sorted[0];
        var avg = sorted.Average();
        var p95 = sorted[(int)Math.Ceiling(0.95 * sorted.Length) - 1];
        var max = sorted[^1];
        var rps = totalMs > 0 ? concurrency / (totalMs / 1000.0) : 0;

        output.WriteLine($"[LA-176] {endpoint,-40} | VUs={concurrency,3} | total={totalMs,6:F1}ms | min={min,5:F1}ms | avg={avg,6:F1}ms | p95={p95,6:F1}ms | max={max,6:F1}ms | rps={rps,6:F1} | err={failures}/{concurrency}");

        if (failures > 0)
        {
            violations.Add($"{endpoint} at concurrency {concurrency} had {failures}/{concurrency} failed responses.");
        }

        if (p95 >= 3000)
        {
            violations.Add($"{endpoint} at concurrency {concurrency} exceeded 3000ms p95: {p95:F1}ms");
        }
    }

    private static async Task<(double[] LatenciesMs, double TotalMs, int Failures)> MeasureAsync(
        string _, int concurrency, Func<Task<HttpResponseMessage>> call, HttpStatusCode expectedCode = HttpStatusCode.OK)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var swTotal = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, concurrency).Select(async _ =>
        {
            await gate.Task;
            var sw = Stopwatch.StartNew();
            using var resp = await call();
            sw.Stop();
            return (sw.Elapsed.TotalMilliseconds, Ok: resp.StatusCode == expectedCode);
        }).ToArray();

        gate.SetResult();
        var results = await Task.WhenAll(tasks);
        swTotal.Stop();

        return (results.Select(r => r.TotalMilliseconds).ToArray(), swTotal.Elapsed.TotalMilliseconds, results.Count(r => !r.Ok));
    }
}
