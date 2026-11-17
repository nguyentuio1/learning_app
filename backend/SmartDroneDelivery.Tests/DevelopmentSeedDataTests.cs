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

    private static async Task<(int Users, int Stations, int Addresses, int Drones, int Packages, int Orders, int StatusLogs, int Missions)>
        CountDevelopmentDataAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (
            await db.Users.CountAsync(),
            await db.LandingStations.CountAsync(),
            await db.CustomerAddresses.CountAsync(),
            await db.Drones.CountAsync(),
            await db.Packages.CountAsync(),
            await db.DeliveryOrders.CountAsync(),
            await db.DeliveryStatusLogs.CountAsync(),
            await db.FlightMissions.CountAsync());
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
    [InlineData("operator2@smartdronedelivery.com", "Operator@123456", "STATION_OPERATOR")]
    [InlineData("manager@smartdronedelivery.com", "Manager@123456", "MANAGER")]
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
            var station = await db.LandingStations.SingleAsync(s => s.StationCode == "HCM-01");
            Assert.Equal(operatorUser.Id, station.OperatorId);
            Assert.Equal("OPERATIONAL", station.Status);

            var operatorUser2 = await db.Users.SingleAsync(u => u.Email == "operator2@smartdronedelivery.com");
            var station2 = await db.LandingStations.SingleAsync(s => s.StationCode == "HCM-02");
            Assert.Equal(operatorUser2.Id, station2.OperatorId);
            Assert.Equal("OPERATIONAL", station2.Status);
        });
    }

    [Fact]
    public async Task Running_the_seed_again_changes_nothing()
    {
        using var factory = new AuthApiFactory();
        await SeedData.InitializeAsync(factory.Services);
        await SeedData.SeedDevelopmentDataAsync(factory.Services);
        var first = await CountDevelopmentDataAsync(factory);
        var slots = 0;
        await factory.WithDbAsync(async db => slots = await db.StationSlots.CountAsync());

        await SeedData.SeedDevelopmentDataAsync(factory.Services);

        Assert.Equal(first, await CountDevelopmentDataAsync(factory));
        await factory.WithDbAsync(async db => Assert.Equal(slots, await db.StationSlots.CountAsync()));
    }

    [Fact]
    public async Task Seeds_demo_addresses_drones_orders_and_mission_with_valid_relations_and_api_results()
    {
        using var factory = new AuthApiFactory();
        await SeedData.InitializeAsync(factory.Services);
        await SeedData.SeedDevelopmentDataAsync(factory.Services);

        await factory.WithDbAsync(async db =>
        {
            var customer = await db.Users.SingleAsync(u => u.Email == "customer@smartdronedelivery.com");
            var dispatcher = await db.Users.SingleAsync(u => u.Email == "dispatcher@smartdronedelivery.com");
            var stations = await db.LandingStations.ToDictionaryAsync(s => s.StationCode);
            var addresses = await db.CustomerAddresses.Where(a => a.UserId == customer.Id).ToListAsync();
            Assert.Equal(2, addresses.Count);
            Assert.Contains(addresses, a => a.Label == "Home - District 1" && a.IsDefault);
            Assert.Contains(addresses, a => a.Label == "Office - Thu Duc" && !a.IsDefault);

            var drones = await db.Drones.Include(d => d.CurrentStation).ToListAsync();
            Assert.Equal(4, drones.Count);
            Assert.Equal(
                new[] { "DRONE-HCM-001", "DRONE-HCM-002", "DRONE-HCM-003", "DRONE-HCM-004" },
                drones.Select(d => d.SerialNumber).OrderBy(s => s).ToArray());
            Assert.Equal(2, drones.Count(d => d.Status == "IDLE"));
            Assert.Single(drones, d => d.Status == "CHARGING");
            Assert.Single(drones, d => d.Status == "MAINTENANCE");
            Assert.All(drones, d => Assert.InRange(d.MaxPayloadGram, 1m, 3000m));
            Assert.All(drones, d => Assert.NotNull(d.CurrentStation));
            Assert.Equal("HCM-01", drones.Single(d => d.SerialNumber == "DRONE-HCM-001").CurrentStation!.StationCode);
            Assert.Equal("HCM-02", drones.Single(d => d.SerialNumber == "DRONE-HCM-002").CurrentStation!.StationCode);
            Assert.Equal("HCM-03", drones.Single(d => d.SerialNumber == "DRONE-HCM-004").CurrentStation!.StationCode);

            var orders = await db.DeliveryOrders.Include(o => o.Package).ToListAsync();
            Assert.Equal(3, orders.Count);
            Assert.Equal(
                new[] { "APPROVED", "DELIVERED", "PENDING" },
                orders.Select(o => o.Status).OrderBy(s => s).ToArray());
            Assert.All(orders, order =>
            {
                Assert.Equal(customer.Id, order.CustomerId);
                Assert.Equal(stations["HCM-01"].Id, order.OriginStationId);
                Assert.Equal(stations["HCM-02"].Id, order.DestinationStationId);
                Assert.Contains(addresses, address => address.Id == order.DeliveryAddressId);
                Assert.Equal(customer.Id, order.Package.SenderId);
            });
            Assert.Equal(3, await db.Packages.CountAsync());
            Assert.Null(orders.Single(o => o.TrackingNumber == "ORD-DEMO-001").DispatcherId);
            Assert.Equal(dispatcher.Id, orders.Single(o => o.TrackingNumber == "ORD-DEMO-002").DispatcherId);
            Assert.Equal(dispatcher.Id, orders.Single(o => o.TrackingNumber == "ORD-DEMO-003").DispatcherId);
            Assert.Equal(9, await db.DeliveryStatusLogs.CountAsync());

            var delivered = orders.Single(o => o.TrackingNumber == "ORD-DEMO-003");
            Assert.Equal(1, await db.FlightMissions.CountAsync());
            var mission = await db.FlightMissions.Include(m => m.Drone).SingleAsync(m => m.OrderId == delivered.Id);
            Assert.Equal("COMPLETED", mission.FlightStatus);
            Assert.Equal("DRONE-HCM-002", mission.Drone.SerialNumber);
            Assert.NotNull(mission.StartTime);
            Assert.NotNull(mission.EndTime);
            Assert.NotNull(delivered.ScheduledDepartureTime);
            Assert.NotNull(delivered.ActualDepartureTime);
            Assert.NotNull(delivered.EstimatedDeliveryTime);
            Assert.NotNull(delivered.ActualDeliveryTime);

            var statusLogs = await db.DeliveryStatusLogs
                .Where(log => log.OrderId == delivered.Id)
                .OrderBy(log => log.CreatedAt)
                .ThenBy(log => log.Id)
                .ToListAsync();
            var stationOperator = await db.Users.SingleAsync(u => u.Email == "operator@smartdronedelivery.com");
            var stationOperator2 = await db.Users.SingleAsync(u => u.Email == "operator2@smartdronedelivery.com");
            Assert.Equal(
                new[] { "PENDING", "APPROVED", "READY_FOR_TAKEOFF", "IN_TRANSIT", "ARRIVED", "DELIVERED" },
                statusLogs.Select(log => log.ToStatus));
            Assert.Equal(
                new Guid?[] { customer.Id, dispatcher.Id, stationOperator.Id, null, null, stationOperator2.Id },
                statusLogs.Select(log => log.ChangedBy));

        });

        var dispatcherClient = factory.CreateClient();
        var dispatcherLogin = await dispatcherClient.PostAsJsonAsync("/api/auth/login",
            new { email = "dispatcher@smartdronedelivery.com", password = "Dispatcher@123456" });
        var dispatcherToken = (await dispatcherLogin.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        dispatcherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", dispatcherToken);
        var dronesResponse = await dispatcherClient.GetAsync("/api/drones");
        Assert.Equal(HttpStatusCode.OK, dronesResponse.StatusCode);
        var droneResults = await dronesResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, droneResults.GetArrayLength());
        Assert.Contains(droneResults.EnumerateArray(),
            drone => drone.GetProperty("serialNumber").GetString() == "DRONE-HCM-004" &&
                     drone.GetProperty("status").GetString() == "MAINTENANCE");

        var customerClient = factory.CreateClient();
        var customerLogin = await customerClient.PostAsJsonAsync("/api/auth/login",
            new { email = "customer@smartdronedelivery.com", password = "Customer@123456" });
        var customerToken = (await customerLogin.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        customerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        var ordersResponse = await customerClient.GetAsync("/api/delivery-orders");
        Assert.Equal(HttpStatusCode.OK, ordersResponse.StatusCode);
        var orderResults = await ordersResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, orderResults.GetProperty("totalCount").GetInt32());
        Assert.Equal(
            new[] { "APPROVED", "DELIVERED", "PENDING" },
            orderResults.GetProperty("items").EnumerateArray()
                .Select(o => o.GetProperty("status").GetString())
                .OrderBy(s => s)
                .ToArray());

    }

    [Fact]
    public async Task Every_seeded_drone_accepts_its_current_specs_through_the_update_api()
    {
        using var factory = new AuthApiFactory();
        await SeedData.InitializeAsync(factory.Services);
        await SeedData.SeedDevelopmentDataAsync(factory.Services);
        using var client = factory.CreateClient();

        using var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = TestSettings.AdminEmail, password = TestSettings.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var dronesResponse = await client.GetAsync("/api/drones");
        Assert.Equal(HttpStatusCode.OK, dronesResponse.StatusCode);
        var drones = await dronesResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, drones.GetArrayLength());

        foreach (var drone in drones.EnumerateArray())
        {
            var id = drone.GetProperty("id").GetString();
            using var updateResponse = await client.PutAsJsonAsync($"/api/drones/{id}", new
            {
                modelName = drone.GetProperty("modelName").GetString(),
                maxPayloadGram = drone.GetProperty("maxPayloadGram").GetDecimal(),
                maxFlightRangeKm = drone.GetProperty("maxFlightRangeKm").GetDecimal(),
                batteryCapacityMah = drone.GetProperty("batteryCapacityMah").GetInt32()
            });
            Assert.True(updateResponse.StatusCode == HttpStatusCode.OK,
                $"Updating {drone.GetProperty("serialNumber").GetString()} returned {(int)updateResponse.StatusCode}.");
        }
    }

    [Fact]
    public async Task Approved_demo_order_is_listed_but_unavailable_for_drone_assignment_until_scheduled()
    {
        using var factory = new AuthApiFactory();
        await SeedData.InitializeAsync(factory.Services);
        await SeedData.SeedDevelopmentDataAsync(factory.Services);
        using var client = factory.CreateClient();

        using var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "dispatcher@smartdronedelivery.com", password = "Dispatcher@123456" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var approvedResponse = await client.GetAsync("/api/dispatch/approved-orders");
        Assert.Equal(HttpStatusCode.OK, approvedResponse.StatusCode);
        var approvedOrders = await approvedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var demoOrder = Assert.Single(approvedOrders.GetProperty("items").EnumerateArray(),
            order => order.GetProperty("trackingNumber").GetString() == "ORD-DEMO-002");

        var orderId = demoOrder.GetProperty("orderId").GetString();
        using var candidatesResponse = await client.GetAsync($"/api/delivery-orders/{orderId}/available-drones");
        Assert.Equal(HttpStatusCode.Conflict, candidatesResponse.StatusCode);
        Assert.Contains("must be APPROVED and scheduled",
            await candidatesResponse.Content.ReadAsStringAsync());
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
        Assert.Equal(6, users);
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
