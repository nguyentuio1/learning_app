using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class AnalyticsApiTests
{
    internal const string Query = "?fromDate=2026-11-15&toDate=2026-11-15";
    internal const string Dashboard = "/api/analytics/dashboard";
    internal const string Report = "/api/analytics/reports/deliveries";

    [Theory]
    [InlineData("/api/analytics/dashboard")]
    [InlineData("/api/analytics/reports/deliveries")]
    public async Task Anonymous_cannot_read_analytics(string route)
    {
        using var factory = new AuthApiFactory(); using var client = factory.CreateClient();
        using var response = await client.GetAsync(route + Query);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("CUSTOMER")][InlineData("ADMIN")][InlineData("DISPATCHER")][InlineData("STATION_OPERATOR")]
    public async Task Nonmanager_roles_are_denied(string role)
    {
        using var factory = new AuthApiFactory(); using var client = factory.CreateClient();
        await AuthorizeAsync(factory.Services, client, role);
        using var response = await client.GetAsync(Dashboard + Query);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Stale_manager_token_does_not_keep_access(bool deactivate)
    {
        using var factory = new AuthApiFactory(); using var client = factory.CreateClient();
        var id = await AuthorizeAsync(factory.Services, client);
        await factory.WithDbAsync(async db => {
            var user = await db.Users.SingleAsync(x => x.Id == id);
            if (deactivate) user.Status = "INACTIVE";
            else user.RoleId = await db.Roles.Where(x => x.Code == "CUSTOMER").Select(x => x.Id).SingleAsync();
            await db.SaveChangesAsync();
        });
        foreach (var route in new[] { Dashboard, Report }) {
            using var response = await client.GetAsync(route + Query);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("?fromDate=2026-11-15")]
    [InlineData("?fromDate=2026-11-16&toDate=2026-11-15")]
    [InlineData("?fromDate=2025-01-01&toDate=2026-01-02")]
    [InlineData("?fromDate=2026-02-30&toDate=2026-03-01")]
    [InlineData("?fromDate=0001-01-01&toDate=0001-01-01")]
    [InlineData("?fromDate=9999-12-31&toDate=9999-12-31")]
    [InlineData("?fromDate=2026-11-15T00:00:00Z&toDate=2026-11-15")]
    [InlineData(Query + "&stationId=123")]
    [InlineData(Query + "&page=1")]
    [InlineData(Query + "&fromDate=2026-11-16")]
    public async Task Invalid_dashboard_query_returns_field_errors(string query)
    {
        using var factory = new AuthApiFactory(); using var client = factory.CreateClient();
        await AuthorizeAsync(factory.Services, client);
        using var response = await client.GetAsync(Dashboard + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(body.GetProperty("errors").EnumerateObject());
    }

    [Theory]
    [InlineData("&page=0")][InlineData("&pageSize=101")][InlineData("&pageSize=0")]
    [InlineData("&page=abc")][InlineData("&page=2147483648")][InlineData("&page=1&page=2")]
    public async Task Invalid_report_pagination_is_rejected(string suffix)
    {
        using var factory = new AuthApiFactory(); using var client = factory.CreateClient();
        await AuthorizeAsync(factory.Services, client);
        using var response = await client.GetAsync(Report + Query + suffix);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Empty_period_returns_zero_counts_and_null_ratios_not_fake_measurements()
    {
        using var factory = new AuthApiFactory(); using var client = factory.CreateClient();
        await AuthorizeAsync(factory.Services, client);
        await AssertEmptyAsync(client);
    }

    internal static async Task AssertEmptyAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Dashboard + Query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("totalOrders").GetInt32());
        var counts = body.GetProperty("ordersByStatus").EnumerateArray().ToList();
        Assert.Equal(8, counts.Count); Assert.All(counts, x => Assert.Equal(0, x.GetProperty("count").GetInt32()));
        Assert.Equal(JsonValueKind.Null, body.GetProperty("successRate").GetProperty("percent").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("averageDelivery").GetProperty("minutes").ValueKind);
        Assert.Empty(body.GetProperty("stationUsage").EnumerateArray());
        using var report = await client.GetAsync(Report + Query);
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        var page = await report.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(0,page.GetProperty("totalCount").GetInt32()); Assert.Equal(0,page.GetProperty("totalPages").GetInt32());
        Assert.Equal(1,page.GetProperty("page").GetInt32()); Assert.Equal(20,page.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Metrics_use_local_day_current_distinct_orders_and_valid_actual_durations()
    {
        using var factory = new AuthApiFactory(); using var client = factory.CreateClient();
        var user = await AuthorizeAsync(factory.Services, client);
        await factory.WithDbAsync(db => SeedCohortAsync(db, user));
        await AssertCohortAsync(client);
    }

    internal static async Task AssertCohortAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Dashboard + Query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(6, body.GetProperty("totalOrders").GetInt32());
        var period = body.GetProperty("period");
        Assert.Equal("2026-11-14T17:00:00Z", period.GetProperty("startUtc").GetString());
        Assert.Equal("2026-11-15T17:00:00Z", period.GetProperty("endExclusiveUtc").GetString());
        var counts = body.GetProperty("ordersByStatus").EnumerateArray().ToDictionary(x => x.GetProperty("status").GetString()!, x => x.GetProperty("count").GetInt32());
        Assert.Equal(3, counts["DELIVERED"]); Assert.Equal(1, counts["FAILED"]); Assert.Equal(1, counts["PENDING"]); Assert.Equal(1, counts["CANCELLED"]);
        Assert.Equal(8, counts.Count); Assert.Equal(6, counts.Values.Sum());
        Assert.Equal(75, body.GetProperty("successRate").GetProperty("percent").GetDecimal());
        var average = body.GetProperty("averageDelivery");
        Assert.Equal(12.5m, average.GetProperty("minutes").GetDecimal());
        Assert.Equal(1, average.GetProperty("sampleCount").GetInt32()); Assert.Equal(2, average.GetProperty("excludedCount").GetInt32());
        var stations = body.GetProperty("stationUsage").EnumerateArray().ToList();
        Assert.Equal(2, stations.Count);
        Assert.All(stations, s => Assert.Equal(6, s.GetProperty("totalDistinctOrders").GetInt32()));
        var raw = body.ToString(); Assert.DoesNotContain("pickupSecureCode", raw); Assert.DoesNotContain("recipientPhone", raw); Assert.DoesNotContain("customerId", raw);
        using var report = await client.GetAsync(Report + Query + "&page=2&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        var page = await report.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(6, page.GetProperty("totalCount").GetInt32()); Assert.Equal(3, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(new[] {"AN-3", "AN-2"}, page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("trackingNumber").GetString()).ToArray());
        using var last = await client.GetAsync(Report + Query + "&page=3&pageSize=2");
        var lastPage = await last.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null,lastPage.GetProperty("items")[0].GetProperty("durationMinutes").ValueKind);
        Assert.Equal(12.5m,lastPage.GetProperty("items")[1].GetProperty("durationMinutes").GetDecimal());
        Assert.DoesNotContain("pickupSecureCode",lastPage.ToString()); Assert.DoesNotContain("recipient",lastPage.ToString());
        using var beyond = await client.GetAsync(Report + Query + "&page=2147483647&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, beyond.StatusCode);
        Assert.Empty((await beyond.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
    }

    internal static async Task<Guid> AuthorizeAsync(IServiceProvider services, HttpClient client, string role = "MANAGER")
    {
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User { Email = $"analytics-{Guid.NewGuid():N}@test.com", FullName = "Analytics", PhoneNumber = Guid.NewGuid().ToString("N")[..15], Role = await db.Roles.SingleAsync(x => x.Code == role), Status = "ACTIVE", PasswordHash = "unused" };
        db.Users.Add(user); await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, DateTime.UtcNow));
        return user.Id;
    }

    internal static async Task SeedCohortAsync(AppDbContext db, Guid user)
    {
        var a = new LandingStation { StationCode = "AN-A", Name = "A", Address = "A" };
        var b = new LandingStation { StationCode = "AN-B", Name = "B", Address = "B" };
        db.LandingStations.AddRange(a,b);
        var start = new DateTime(2026,11,14,17,0,0,DateTimeKind.Utc);
        var statuses = new[] { "DELIVERED", "DELIVERED", "DELIVERED", "FAILED", "CANCELLED", "PENDING", "DELIVERED", "DELIVERED" };
        for (var i=0; i<8; i++) {
            var p = new Package { SenderId = user, PackageName = "test", WeightGram = 100 };
            var o = new DeliveryOrder { CustomerId = user, Package = p, OriginStation = a, DestinationStation = b, TrackingNumber = $"AN-{i}", Status = statuses[i], PickupSecureCode = "123456", RecipientName = "Secret", RecipientPhone = "0900000000", CreatedAt = i==6 ? start.AddMilliseconds(-1) : i==7 ? start.AddDays(1) : start.AddHours(i), ActualDepartureTime = start.AddHours(1), ActualDeliveryTime = i==0 ? start.AddHours(1).AddMinutes(12.5) : i==1 ? null : start };
            // Repeated status history must not multiply an order.
            o.StatusLogs.Add(new DeliveryStatusLog { ToStatus = statuses[i], CreatedAt = start });
            o.StatusLogs.Add(new DeliveryStatusLog { ToStatus = statuses[i], CreatedAt = start.AddMinutes(1) });
            db.DeliveryOrders.Add(o);
        }
        await db.SaveChangesAsync();
    }
}
