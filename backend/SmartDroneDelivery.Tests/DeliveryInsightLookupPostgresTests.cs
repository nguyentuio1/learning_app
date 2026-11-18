using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SmartDroneDelivery.Tests;

public class DeliveryInsightLookupPostgresTests
{
    [PostgresFact]
    public async Task Real_postgres_translates_tracking_search_day_bounds_and_pagination()
    {
        using var factory = new PostgresApiFactory();
        using var client = factory.CreateClient();
        var customer = await AnalyticsApiTests.AuthorizeAsync(factory.Services, client, "CUSTOMER");
        await factory.WithDbAsync(db => AnalyticsApiTests.SeedCohortAsync(db, customer));
        await AnalyticsApiTests.AuthorizeAsync(factory.Services, client, "MANAGER");
        const string route = "/api/delivery-insights/orders";
        const string filter = "?searchKeyword=an-&status=delivered&fromDate=2026-11-15&toDate=2026-11-15&pageSize=2";
        using var response = await client.GetAsync(route + filter);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(new[] { "AN-2", "AN-1" }, page.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("trackingNumber").GetString()).ToArray());
        Assert.DoesNotContain("Secret", page.ToString());
        Assert.DoesNotContain("123456", page.ToString());
        using var second = await client.GetAsync(route + filter + "&page=2");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var next = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AN-0", Assert.Single(next.GetProperty("items").EnumerateArray()).GetProperty("trackingNumber").GetString());
        using var huge = await client.GetAsync(route + "?page=2147483647&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, huge.StatusCode);
        Assert.Empty((await huge.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
    }
}
