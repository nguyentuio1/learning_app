using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Tests;

public class AnalyticsAcceptanceTests
{
    [Fact]
    public async Task Inclusive_366_day_window_succeeds_while_367_days_is_rejected()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        await AnalyticsApiTests.AuthorizeAsync(factory.Services, client);

        using var ok = await client.GetAsync($"{AnalyticsApiTests.Dashboard}?fromDate=2026-01-01&toDate=2027-01-01");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        using var tooWide = await client.GetAsync($"{AnalyticsApiTests.Dashboard}?fromDate=2026-01-01&toDate=2027-01-02");
        Assert.Equal(HttpStatusCode.BadRequest, tooWide.StatusCode);
    }

    [Fact]
    public async Task Same_station_order_is_counted_once_in_total_distinct_orders()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var user = await AnalyticsApiTests.AuthorizeAsync(factory.Services, client);

        await factory.WithDbAsync(async db =>
        {
            var station = new LandingStation { StationCode = "AN-SAME", Name = "Same", Address = "Same" };
            var pkg = new Package { SenderId = user, PackageName = "loop", WeightGram = 100 };
            db.LandingStations.Add(station);
            db.Packages.Add(pkg);
            db.DeliveryOrders.Add(new DeliveryOrder
            {
                CustomerId = user,
                Package = pkg,
                OriginStation = station,
                DestinationStation = station,
                TrackingNumber = "AN-LOOP-1",
                Status = "DELIVERED",
                PickupSecureCode = "111111",
                RecipientName = "Hidden",
                RecipientPhone = "0900000001",
                CreatedAt = new DateTime(2026, 11, 15, 3, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();
        });

        using var response = await client.GetAsync(AnalyticsApiTests.Dashboard + AnalyticsApiTests.Query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var usage = Assert.Single(body.GetProperty("stationUsage").EnumerateArray());
        Assert.Equal(1, usage.GetProperty("originOrderCount").GetInt32());
        Assert.Equal(1, usage.GetProperty("destinationOrderCount").GetInt32());
        Assert.Equal(1, usage.GetProperty("totalDistinctOrders").GetInt32());
    }

    [Fact]
    public async Task Failed_only_cohort_returns_zero_success_rate_and_null_average_duration()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        var user = await AnalyticsApiTests.AuthorizeAsync(factory.Services, client);

        await factory.WithDbAsync(async db =>
        {
            var a = new LandingStation { StationCode = "AN-F1", Name = "F1", Address = "F1" };
            var b = new LandingStation { StationCode = "AN-F2", Name = "F2", Address = "F2" };
            var pkg = new Package { SenderId = user, PackageName = "fail", WeightGram = 100 };
            db.LandingStations.AddRange(a, b);
            db.Packages.Add(pkg);
            db.DeliveryOrders.Add(new DeliveryOrder
            {
                CustomerId = user,
                Package = pkg,
                OriginStation = a,
                DestinationStation = b,
                TrackingNumber = "AN-FAIL-1",
                Status = "FAILED",
                PickupSecureCode = "222222",
                RecipientName = "Hidden",
                RecipientPhone = "0900000002",
                CreatedAt = new DateTime(2026, 11, 15, 4, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();
        });

        using var response = await client.GetAsync(AnalyticsApiTests.Dashboard + AnalyticsApiTests.Query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var rate = body.GetProperty("successRate");
        Assert.Equal(0, rate.GetProperty("deliveredCount").GetInt32());
        Assert.Equal(1, rate.GetProperty("failedCount").GetInt32());
        Assert.Equal(0m, rate.GetProperty("percent").GetDecimal());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("averageDelivery").GetProperty("minutes").ValueKind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?fromDate=2026-11-16&toDate=2026-11-15")]
    [InlineData("?fromDate=2026-11-15&toDate=2026-11-15&unknown=1")]
    public async Task Invalid_report_date_range_or_unknown_param_returns_bad_request(string query)
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        await AnalyticsApiTests.AuthorizeAsync(factory.Services, client);

        using var response = await client.GetAsync(AnalyticsApiTests.Report + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
