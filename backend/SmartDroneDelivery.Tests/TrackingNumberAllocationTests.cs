using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class TrackingNumberAllocationTests : IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;
    private static int _seed;
    public TrackingNumberAllocationTests(PostgresApiFactory factory) {
        _factory = factory;
        // Bound database connections, not HTTP concurrency, on shared PostgreSQL test hosts.
        _factory.MaximumPoolSize = 24;
    }

    [PostgresFact]
    public async Task Upgrade_preserves_existing_numbers_and_continues_numerically_past_9999()
    {
        using var client = _factory.CreateClient();
        var (customer, origin, destination, packages, _) = await SeedAsync(client, 4);
        var month = DateTime.UtcNow.ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture);
        var previous = DateTime.UtcNow.AddMonths(-1).ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture);
        await _factory.WithDbAsync(async db =>
        {
            await db.GetService<IMigrator>().MigrateAsync("20261117110649_AddAuditLogSearchIndexes");
            DeliveryOrder Legacy(Guid package, string number) => new() { Id = Guid.NewGuid(),
                CustomerId = customer, PackageId = package, OriginStationId = origin, DestinationStationId = destination,
                TrackingNumber = number, RecipientName = "Synthetic legacy", RecipientPhone = "0901234567",
                PickupSecureCode = "123456", Status = "PENDING" };
            var numbers = new[] { $"DD-{month}-9999", $"DD-{month}-10000", $"DD-{previous}-0042" };
            db.DeliveryOrders.AddRange(numbers.Select((n, i) => Legacy(packages[i], n)));
            await db.SaveChangesAsync();
            await db.Database.MigrateAsync();
            Assert.Equal(10000, (await db.DeliveryTrackingCounters.SingleAsync(c => c.Month == month)).LastValue);
            Assert.Equal(42, (await db.DeliveryTrackingCounters.SingleAsync(c => c.Month == previous)).LastValue);
            var created = await new DeliveryOrderService(db).CreateAsync(customer, new CreateDeliveryOrderRequest(
                packages[3], origin, destination, null, "Synthetic recipient", "0901234567"));
            Assert.Equal($"DD-{month}-10001", created.TrackingNumber);
            Assert.Equal(3, await db.DeliveryOrders.CountAsync(o => numbers.Contains(o.TrackingNumber)));
        });
    }

    [PostgresFact]
    public async Task Concurrent_reuse_of_one_package_has_one_winner_and_no_partial_artifacts()
    {
        using var client = _factory.CreateClient();
        var (customer, origin, destination, packages, token) = await SeedAsync(client, 1);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            await gate.Task;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/delivery-orders");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new { packageId = packages[0], originStationId = origin,
                destinationStationId = destination, recipientName = "Synthetic recipient", recipientPhone = "0901234567" });
            return await client.SendAsync(request);
        }).ToArray();
        gate.SetResult();
        var responses = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(90));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict));
            await _factory.WithDbAsync(async db =>
            {
                Assert.Equal(1, await db.DeliveryOrders.CountAsync(o => o.CustomerId == customer));
                Assert.Equal(1, await db.DeliveryStatusLogs.CountAsync(l => l.ChangedBy == customer));
                Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == customer));
            });
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [PostgresFact]
    public async Task One_hundred_parallel_creations_have_unique_numbers_and_complete_artifacts()
    {
        using var client = _factory.CreateClient();
        var (customer, origin, destination, packages, token) = await SeedAsync(client, 100);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = packages.Select(async package =>
        {
            await gate.Task;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/delivery-orders");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new
            {
                packageId = package, originStationId = origin, destinationStationId = destination,
                recipientName = "Synthetic recipient", recipientPhone = "0901234567"
            });
            return await client.SendAsync(request);
        }).ToArray();
        gate.SetResult();
        var responses = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(90));
        try
        {
            var failures = responses.Where(r => r.StatusCode != HttpStatusCode.Created)
                .GroupBy(r => r.StatusCode).Select(g => $"{(int)g.Key}: {g.Count()}");
            var serverError = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.InternalServerError);
            var diagnostic = serverError is null ? "" : string.Join(" | ", (await serverError.Content.ReadAsStringAsync()).Split('\n').Take(8));
            Assert.True(responses.All(r => r.StatusCode == HttpStatusCode.Created),
                $"Expected 100 successful creations; failures: {string.Join(", ", failures)}; {diagnostic}");
            await _factory.WithDbAsync(async db =>
            {
                var orders = await db.DeliveryOrders.Where(o => o.CustomerId == customer).ToListAsync();
                Assert.Equal(100, orders.Count);
                Assert.Equal(100, orders.Select(o => o.TrackingNumber).Distinct().Count());
                Assert.All(orders, o => Assert.Matches(@"^DD-\d{6}-\d{4,}$", o.TrackingNumber));
                Assert.Equal(100, await db.DeliveryStatusLogs.CountAsync(l => l.ChangedBy == customer));
                Assert.Equal(100, await db.Notifications.CountAsync(n => n.UserId == customer));
            });
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    private async Task<(Guid Customer, Guid Origin, Guid Destination, Guid[] Packages, string Token)>
        SeedAsync(HttpClient client, int count)
    {
        var customer = Guid.NewGuid(); var origin = Guid.NewGuid(); var destination = Guid.NewGuid();
        var packages = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
        var email = $"tracking-{customer:N}@example.test";
        const string password = "Synthetic-Test-123";
        await _factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.SingleAsync(r => r.Code == "CUSTOMER");
            db.Users.Add(new User { Id = customer, Email = email, FullName = "Synthetic customer",
                PhoneNumber = $"08{Interlocked.Increment(ref _seed):D8}", PasswordHash = new PasswordHasher().Hash(password), RoleId = role.Id });
            LandingStation Station(Guid id) => new() { Id = id, StationCode = $"T-{id.ToString("N")[..12]}",
                Name = "Synthetic station", Address = "Test only", Latitude = 10.77m, Longitude = 106.70m };
            db.LandingStations.AddRange(Station(origin), Station(destination));
            db.Packages.AddRange(packages.Select(id => new Package { Id = id, SenderId = customer,
                PackageName = "Synthetic package", WeightGram = 500m, LengthCm = 20m, WidthCm = 15m, HeightCm = 10m }));
            await db.SaveChangesAsync();
        });
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return (customer, origin, destination, packages, body.GetProperty("accessToken").GetString()!);
    }
}
