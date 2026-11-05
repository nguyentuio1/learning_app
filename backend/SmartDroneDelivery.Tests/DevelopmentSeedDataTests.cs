using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Data;

namespace SmartDroneDelivery.Tests;

/// <summary>LA-325: demo stations and accounts for local development (README, "Demo accounts").</summary>
public class DevelopmentSeedDataTests
{
    private static WebApplicationFactory<Program> WithSeedFlag(AuthApiFactory factory, bool enabled, string? environment = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            if (environment != null) builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DevelopmentData"] = enabled ? "true" : "false" }));
        });

    private static async Task<(int Stations, int Users)> CountAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.LandingStations.CountAsync(), await db.Users.CountAsync());
    }

    [Fact]
    public async Task Test_factory_does_not_seed_demo_data_so_existing_tests_stay_isolated()
    {
        using var factory = new AuthApiFactory();
        factory.CreateClient();

        var (stations, users) = await CountAsync(factory);

        Assert.Equal(0, stations);
        Assert.Equal(1, users); // only the administrator of the base seed
    }

    [Fact]
    public async Task Seeds_two_operational_stations_and_one_in_maintenance_with_empty_slots()
    {
        using var factory = new AuthApiFactory();
        await SeedData.InitializeAsync(factory.Services);

        await SeedData.SeedDevelopmentDataAsync(factory.Services);

        await factory.WithDbAsync(async db =>
        {
            var stations = await db.LandingStations.Include(s => s.Slots).ToListAsync();
            Assert.Equal(2, stations.Count(s => s.Status == "OPERATIONAL"));
            Assert.Equal(1, stations.Count(s => s.Status == "MAINTENANCE"));
            Assert.All(stations, s =>
            {
                Assert.Equal(s.TotalSlots, s.Slots.Count);
                Assert.All(s.Slots, slot => Assert.Equal("EMPTY", slot.Status));
                Assert.Equal(Enumerable.Range(1, s.TotalSlots), s.Slots.Select(slot => slot.SlotNumber).OrderBy(n => n));
            });
        });
    }

    [Theory]
    [InlineData("customer@smartdronedelivery.com", "Customer@123456", "CUSTOMER")]
    [InlineData("dispatcher@smartdronedelivery.com", "Dispatcher@123456", "DISPATCHER")]
    [InlineData("operator@smartdronedelivery.com", "Operator@123456", "STATION_OPERATOR")]
    public async Task Seeds_active_demo_accounts_that_can_sign_in(string email, string password, string role)
    {
        using var factory = new AuthApiFactory();
        using var seeded = WithSeedFlag(factory, enabled: true);
        var client = seeded.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(role, body.GetProperty("user").GetProperty("role").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Station_operator_is_assigned_to_the_first_operational_station()
    {
        using var factory = new AuthApiFactory();
        await SeedData.InitializeAsync(factory.Services);

        await SeedData.SeedDevelopmentDataAsync(factory.Services);

        await factory.WithDbAsync(async db =>
        {
            var operatorUser = await db.Users.SingleAsync(u => u.Email == "operator@smartdronedelivery.com");
            var station = await db.LandingStations.SingleAsync(s => s.OperatorId == operatorUser.Id);
            Assert.Equal("OPERATIONAL", station.Status);
        });
    }

    [Fact]
    public async Task Running_the_seed_again_changes_nothing()
    {
        using var factory = new AuthApiFactory();
        await SeedData.InitializeAsync(factory.Services);
        await SeedData.SeedDevelopmentDataAsync(factory.Services);
        var first = await CountAsync(factory);
        var slots = 0;
        await factory.WithDbAsync(async db => slots = await db.StationSlots.CountAsync());

        await SeedData.SeedDevelopmentDataAsync(factory.Services);
        await SeedData.SeedDevelopmentDataAsync(factory.Services);

        Assert.Equal(first, await CountAsync(factory));
        await factory.WithDbAsync(async db => Assert.Equal(slots, await db.StationSlots.CountAsync()));
    }

    [Fact]
    public async Task Existing_station_and_user_are_kept_untouched_when_seeding_again()
    {
        using var factory = new AuthApiFactory();
        await SeedData.InitializeAsync(factory.Services);
        await SeedData.SeedDevelopmentDataAsync(factory.Services);
        await factory.WithDbAsync(async db =>
        {
            (await db.LandingStations.FirstAsync(s => s.StationCode == "HCM-01")).Name = "Renamed by hand";
            (await db.Users.FirstAsync(u => u.Email == "customer@smartdronedelivery.com")).FullName = "Changed by hand";
            await db.SaveChangesAsync();
        });

        await SeedData.SeedDevelopmentDataAsync(factory.Services);

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal("Renamed by hand", (await db.LandingStations.FirstAsync(s => s.StationCode == "HCM-01")).Name);
            Assert.Equal("Changed by hand", (await db.Users.FirstAsync(u => u.Email == "customer@smartdronedelivery.com")).FullName);
        });
    }

    [Fact]
    public async Task Program_seeds_demo_data_in_development_when_the_flag_is_on()
    {
        using var factory = new AuthApiFactory();
        using var seeded = WithSeedFlag(factory, enabled: true);
        seeded.CreateClient();

        var (stations, users) = await CountAsync(seeded);

        Assert.Equal(3, stations);
        Assert.Equal(4, users);
    }

    [Fact]
    public async Task Program_never_seeds_demo_data_in_production_even_when_the_flag_is_on()
    {
        using var factory = new AuthApiFactory();
        using var production = WithSeedFlag(factory, enabled: true, environment: "Production");
        production.CreateClient();

        var (stations, users) = await CountAsync(production);

        Assert.Equal(0, stations);
        Assert.Equal(1, users);
    }

    [Fact]
    public async Task Seeded_customer_can_create_an_order_without_any_manual_sql()
    {
        using var factory = new AuthApiFactory();
        using var seeded = WithSeedFlag(factory, enabled: true);
        var client = seeded.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "customer@smartdronedelivery.com", password = "Customer@123456" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var stations = await client.GetFromJsonAsync<JsonElement>("/api/landing-stations?status=OPERATIONAL");
        var ids = stations.EnumerateArray().Select(s => s.GetProperty("id").GetString()!).ToList();
        Assert.Equal(2, ids.Count);

        var package = await client.PostAsJsonAsync("/api/packages",
            new { packageName = "Birthday gift", weightGram = 850, lengthCm = 25, widthCm = 18, heightCm = 10, isFragile = true });
        Assert.Equal(HttpStatusCode.Created, package.StatusCode);
        var packageId = (await package.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var order = await client.PostAsJsonAsync("/api/delivery-orders", new
        {
            packageId,
            originStationId = ids[0],
            destinationStationId = ids[1],
            deliveryAddressId = (string?)null,
            recipientName = "Tran Thi Binh",
            recipientPhone = "0912345678"
        });

        Assert.Equal(HttpStatusCode.Created, order.StatusCode);
    }
}
