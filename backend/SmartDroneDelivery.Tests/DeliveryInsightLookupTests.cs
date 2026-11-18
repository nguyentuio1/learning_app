using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Tests;

public class DeliveryInsightLookupTests : IDisposable
{
    private const string Route = "/api/delivery-insights/orders";
    private readonly AuthApiFactory _factory = new();
    private readonly HttpClient _client;

    public DeliveryInsightLookupTests() => _client = _factory.CreateClient();
    public void Dispose() { _client.Dispose(); _factory.Dispose(); }

    [Fact]
    public async Task Anonymous_is_denied()
    {
        using var response = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("CUSTOMER")][InlineData("ADMIN")][InlineData("STATION_OPERATOR")]
    public async Task Other_roles_do_not_gain_staff_lookup(string role)
    {
        await AnalyticsApiTests.AuthorizeAsync(_factory.Services, _client, role);
        using var response = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("MANAGER")][InlineData("DISPATCHER")]
    public async Task Staff_find_active_and_historical_deliveries_without_private_fields(string role)
    {
        await SeedAsync(role);
        using var response = await _client.GetAsync(Route + "?searchKeyword=%20lookup-%20&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(5, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, body.GetProperty("totalPages").GetInt32());
        var items = body.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new[] { "LOOKUP-001", "LOOKUP-002" }, items.Select(x => x.GetProperty("trackingNumber").GetString()));
        Assert.Equal("DELIVERED", items[0].GetProperty("status").GetString());
        Assert.Equal("IN_TRANSIT", items[1].GetProperty("status").GetString());
        var expected = new[] { "id", "trackingNumber", "status", "originStationName", "destinationStationName", "createdAt" }.Order().ToArray();
        foreach (var item in items)
        {
            Assert.Equal(expected, item.EnumerateObject().Select(x => x.Name).Order().ToArray());
            Assert.Equal("Origin hub", item.GetProperty("originStationName").GetString());
            Assert.EndsWith("Z", item.GetProperty("createdAt").GetString());
        }
        Assert.DoesNotContain("secret", body.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("654321", body.ToString());
    }

    [Fact]
    public async Task Dates_are_inclusive_creation_days_in_utc_plus_seven()
    {
        await SeedAsync();
        using var response = await _client.GetAsync(Route + "?fromDate=2026-11-18&toDate=2026-11-18&status=%20delivered%20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal("LOOKUP-001", body.GetProperty("items")[0].GetProperty("trackingNumber").GetString());
    }

    [Fact]
    public async Task Empty_and_large_pages_are_safe_and_keep_correct_counts()
    {
        await SeedAsync();
        using var noMatch = await _client.GetAsync(Route + "?searchKeyword=missing");
        Assert.Equal(HttpStatusCode.OK, noMatch.StatusCode);
        var empty = await noMatch.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        Assert.Equal(0, empty.GetProperty("totalCount").GetInt32());
        using var response = await _client.GetAsync(Route + "?page=2147483647&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(body.GetProperty("items").EnumerateArray());
        Assert.Equal(5, body.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("?page=0", "page")][InlineData("?page=abc", "page")]
    [InlineData("?page=2147483648", "page")][InlineData("?pageSize=0", "pageSize")]
    [InlineData("?pageSize=101", "pageSize")][InlineData("?status=UNKNOWN", "status")]
    [InlineData("?fromDate=2026-02-30", "fromDate")]
    [InlineData("?fromDate=2026-11-19&toDate=2026-11-18", "toDate")]
    [InlineData("?fromDate=0001-01-01", "fromDate")]
    [InlineData("?toDate=9999-12-31", "toDate")]
    [InlineData("?fromDate=2026-11-18T00:00:00Z", "fromDate")]
    [InlineData("?page=1&page=2", "page")][InlineData("?recipientPhone=x", "recipientPhone")]
    [InlineData("?Page=1", "Page")]
    [InlineData("?searchKeyword=123456789012345678901234567890123456789012345678901", "searchKeyword")]
    public async Task Invalid_queries_have_field_errors(string query, string field)
    {
        await AnalyticsApiTests.AuthorizeAsync(_factory.Services, _client);
        using var response = await _client.GetAsync(Route + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Stale_tokens_do_not_preserve_staff_access(bool deactivate)
    {
        var id = await AnalyticsApiTests.AuthorizeAsync(_factory.Services, _client);
        await _factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == id);
            if (deactivate) user.Status = "INACTIVE";
            else user.RoleId = await db.Roles.Where(x => x.Code == "CUSTOMER").Select(x => x.Id).SingleAsync();
            await db.SaveChangesAsync();
        });
        using var response = await _client.GetAsync(Route + "?page=bad");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Existing_customer_order_list_stays_customer_only()
    {
        await AnalyticsApiTests.AuthorizeAsync(_factory.Services, _client);
        using var response = await _client.GetAsync("/api/delivery-orders");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task SeedAsync(string role = "MANAGER")
    {
        var customer = await AnalyticsApiTests.AuthorizeAsync(_factory.Services, _client, "CUSTOMER");
        var other = await AnalyticsApiTests.AuthorizeAsync(_factory.Services, _client, "CUSTOMER");
        await AnalyticsApiTests.AuthorizeAsync(_factory.Services, _client, role);
        await _factory.WithDbAsync(async db =>
        {
            var origin = new LandingStation { StationCode = "LOOKUP-A", Name = "Origin hub", Address = "Secret address" };
            var destination = new LandingStation { StationCode = "LOOKUP-B", Name = "Destination hub", Address = "Secret address" };
            var statuses = new[] { "DELIVERED", "IN_TRANSIT", "CANCELLED", "FAILED", "DELIVERED" };
            var start = new DateTime(2026, 11, 17, 17, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < statuses.Length; i++)
            {
                var owner = i % 2 == 0 ? customer : other;
                db.DeliveryOrders.Add(new DeliveryOrder
                {
                    Id = Guid.Parse($"00000000-0000-0000-0000-{i + 1:D12}"),
                    CustomerId = owner, Package = new Package { SenderId = owner, PackageName = "Secret package" },
                    OriginStation = origin, DestinationStation = destination,
                    TrackingNumber = $"LOOKUP-{i + 1:D3}", Status = statuses[i],
                    CreatedAt = i < 2 ? start : start.AddMilliseconds(-i),
                    RecipientName = "Secret recipient", RecipientPhone = "0900000000", PickupSecureCode = "654321"
                });
            }
            await db.SaveChangesAsync();
        });
    }
}
