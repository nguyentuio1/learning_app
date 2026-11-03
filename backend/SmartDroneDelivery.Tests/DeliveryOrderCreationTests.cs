using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Tests;

/// <summary>LA-103: POST /api/delivery-orders and GET /api/landing-stations (docs/api/customer-order-api.md sections 4 and 5.1).</summary>
public class DeliveryOrderCreationTests : IClassFixture<AuthApiFactory>
{
    private const string Password = "Passw0rd123";
    private static int _counter;

    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public DeliveryOrderCreationTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ---------- helpers ----------

    private record Customer(Guid Id, string Access);

    private async Task<Customer> NewCustomerAsync()
    {
        var n = Interlocked.Increment(ref _counter);
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = $"orders{n}@example.com", password = Password, fullName = "Nguyen Van An", phoneNumber = $"06{n:D8}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Customer(
            Guid.Parse(auth.GetProperty("user").GetProperty("id").GetString()!),
            auth.GetProperty("accessToken").GetString()!);
    }

    private async Task<LandingStation> NewStationAsync(string status = "OPERATIONAL", string? name = null)
    {
        var n = Interlocked.Increment(ref _counter);
        var station = new LandingStation
        {
            StationCode = $"T-{n:D4}",
            Name = name ?? $"Station {n}",
            Address = $"{n} Test Street",
            Latitude = 10.77m,
            Longitude = 106.70m,
            Status = status
        };
        await _factory.WithDbAsync(async db =>
        {
            db.LandingStations.Add(station);
            await db.SaveChangesAsync();
        });
        return station;
    }

    private async Task<Package> NewPackageAsync(Guid senderId, decimal weight = 850, decimal length = 25, decimal width = 18, decimal height = 10)
    {
        var package = new Package
        {
            SenderId = senderId,
            PackageName = "Birthday gift",
            WeightGram = weight,
            LengthCm = length,
            WidthCm = width,
            HeightCm = height,
            IsFragile = true
        };
        await _factory.WithDbAsync(async db =>
        {
            db.Packages.Add(package);
            await db.SaveChangesAsync();
        });
        return package;
    }

    private async Task<CustomerAddress> NewAddressAsync(Guid userId)
    {
        var address = new CustomerAddress
        {
            UserId = userId,
            Label = "Home",
            ContactName = "Nguyen Van An",
            ContactPhone = "0901234567",
            AddressLine = "12 Nguyen Hue, District 1"
        };
        await _factory.WithDbAsync(async db =>
        {
            db.CustomerAddresses.Add(address);
            await db.SaveChangesAsync();
        });
        return address;
    }

    private static object Body(Package package, LandingStation origin, LandingStation destination,
        Guid? addressId = null, string recipientName = "Tran Thi Binh", string recipientPhone = "0912345678") => new
    {
        packageId = package.Id,
        originStationId = origin.Id,
        destinationStationId = destination.Id,
        deliveryAddressId = addressId,
        recipientName,
        recipientPhone
    };

    private Task<HttpResponseMessage> PostOrderAsync(string? bearer, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/delivery-orders") { Content = JsonContent.Create(body) };
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return _client.SendAsync(request);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    // ---------- success ----------

    [Fact]
    public async Task Create_returns_201_with_a_pending_order_and_the_pickup_code()
    {
        var c = await NewCustomerAsync();
        var package = await NewPackageAsync(c.Id);
        var origin = await NewStationAsync(name: "District 1 Hub");
        var destination = await NewStationAsync(name: "Thu Duc Hub");

        var response = await PostOrderAsync(c.Access, Body(package, origin, destination));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await JsonAsync(response);
        var id = json.GetProperty("id").GetString()!;
        Assert.Equal($"/api/delivery-orders/{id}", response.Headers.Location!.OriginalString);
        Assert.Equal("PENDING", json.GetProperty("status").GetString());
        Assert.Equal("Tran Thi Binh", json.GetProperty("recipientName").GetString());
        Assert.Equal(package.Id.ToString(), json.GetProperty("packageId").GetString());
        Assert.Equal("Birthday gift", json.GetProperty("packageName").GetString());
        Assert.Equal("District 1 Hub", json.GetProperty("originStationName").GetString());
        Assert.Equal("Thu Duc Hub", json.GetProperty("destinationStationName").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("deliveryAddress").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("cancelReason").ValueKind);
        Assert.Matches(@"^\d{6}$", json.GetProperty("pickupSecureCode").GetString()!);

        var logs = json.GetProperty("statusLogs");
        Assert.Equal(1, logs.GetArrayLength());
        Assert.Equal(JsonValueKind.Null, logs[0].GetProperty("fromStatus").ValueKind);
        Assert.Equal("PENDING", logs[0].GetProperty("toStatus").GetString());
    }

    [Fact]
    public async Task Create_saves_the_order_the_first_status_log_and_the_notification_together()
    {
        var c = await NewCustomerAsync();
        var package = await NewPackageAsync(c.Id);
        var origin = await NewStationAsync();
        var destination = await NewStationAsync();

        var json = await JsonAsync(await PostOrderAsync(c.Access, Body(package, origin, destination)));
        var orderId = Guid.Parse(json.GetProperty("id").GetString()!);

        await _factory.WithDbAsync(async db =>
        {
            var order = await db.DeliveryOrders.SingleAsync(o => o.Id == orderId);
            Assert.Equal(c.Id, order.CustomerId);
            Assert.Equal("PENDING", order.Status);
            Assert.Equal(json.GetProperty("pickupSecureCode").GetString(), order.PickupSecureCode);

            var log = await db.DeliveryStatusLogs.SingleAsync(l => l.OrderId == orderId);
            Assert.Null(log.FromStatus);
            Assert.Equal("PENDING", log.ToStatus);
            Assert.Equal(c.Id, log.ChangedBy);

            var notification = await db.Notifications.SingleAsync(n => n.OrderId == orderId);
            Assert.Equal(c.Id, notification.UserId);
            // The pickup code is shown once in the response; it is not copied into the notification (contract section 8, point 5).
            Assert.DoesNotContain(order.PickupSecureCode, notification.Message);
        });
    }

    [Fact]
    public async Task Create_generates_tracking_numbers_in_the_DD_YYYYMM_NNNN_format_in_sequence()
    {
        var c = await NewCustomerAsync();
        var origin = await NewStationAsync();
        var destination = await NewStationAsync();
        var month = DateTime.UtcNow.ToString("yyyyMM");

        var first = await JsonAsync(await PostOrderAsync(c.Access, Body(await NewPackageAsync(c.Id), origin, destination)));
        var second = await JsonAsync(await PostOrderAsync(c.Access, Body(await NewPackageAsync(c.Id), origin, destination)));

        var t1 = first.GetProperty("trackingNumber").GetString()!;
        var t2 = second.GetProperty("trackingNumber").GetString()!;
        Assert.Matches($@"^DD-{month}-\d{{4}}$", t1);
        Assert.Matches($@"^DD-{month}-\d{{4}}$", t2);
        Assert.Equal(int.Parse(t1[^4..]) + 1, int.Parse(t2[^4..]));
    }

    [Fact]
    public async Task Create_with_a_delivery_address_returns_the_address()
    {
        var c = await NewCustomerAsync();
        var package = await NewPackageAsync(c.Id);
        var address = await NewAddressAsync(c.Id);
        var origin = await NewStationAsync();
        var destination = await NewStationAsync();

        var response = await PostOrderAsync(c.Access, Body(package, origin, destination, address.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.Equal(address.Id.ToString(), json.GetProperty("deliveryAddressId").GetString());
        Assert.Equal("12 Nguyen Hue, District 1", json.GetProperty("deliveryAddress").GetProperty("addressLine").GetString());
    }

    // ---------- 400 ----------

    [Theory]
    [InlineData("", "0912345678", "recipientName")]
    [InlineData("   ", "0912345678", "recipientName")]
    [InlineData("Tran Thi Binh", "12345", "recipientPhone")]
    [InlineData("Tran Thi Binh", "09123456789012", "recipientPhone")]
    [InlineData("Tran Thi Binh", "09123abc78", "recipientPhone")]
    public async Task Create_rejects_invalid_recipient_data(string name, string phone, string field)
    {
        var c = await NewCustomerAsync();
        var package = await NewPackageAsync(c.Id);
        var origin = await NewStationAsync();
        var destination = await NewStationAsync();

        var response = await PostOrderAsync(c.Access, Body(package, origin, destination, null, name, phone));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Create_rejects_a_recipient_name_longer_than_150_characters()
    {
        var c = await NewCustomerAsync();
        var package = await NewPackageAsync(c.Id);
        var origin = await NewStationAsync();
        var destination = await NewStationAsync();

        var response = await PostOrderAsync(c.Access, Body(package, origin, destination, null, new string('a', 151)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("recipientName", out _));
    }

    [Fact]
    public async Task Create_rejects_the_same_origin_and_destination()
    {
        var c = await NewCustomerAsync();
        var package = await NewPackageAsync(c.Id);
        var station = await NewStationAsync();

        var response = await PostOrderAsync(c.Access, Body(package, station, station));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("destinationStationId", out _));
    }

    [Theory]
    [InlineData("origin")]
    [InlineData("destination")]
    public async Task Create_rejects_a_station_that_is_not_operational(string which)
    {
        var c = await NewCustomerAsync();
        var package = await NewPackageAsync(c.Id);
        var origin = await NewStationAsync(which == "origin" ? "MAINTENANCE" : "OPERATIONAL");
        var destination = await NewStationAsync(which == "destination" ? "OFFLINE" : "OPERATIONAL");

        var response = await PostOrderAsync(c.Access, Body(package, origin, destination));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty($"{which}StationId", out _));
    }

    [Theory]
    [InlineData(3001, 25, 18, 10, "weightGram")]
    [InlineData(850, 31, 18, 10, "lengthCm")]
    [InlineData(850, 25, 21, 10, "widthCm")]
    [InlineData(850, 25, 18, 16, "heightCm")]
    public async Task Create_rejects_a_package_over_the_drone_limits(decimal weight, decimal length, decimal width, decimal height, string field)
    {
        var c = await NewCustomerAsync();
        var package = await NewPackageAsync(c.Id, weight, length, width, height);
        var origin = await NewStationAsync();
        var destination = await NewStationAsync();

        var response = await PostOrderAsync(c.Access, Body(package, origin, destination));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Create_accepts_a_package_exactly_on_the_drone_limits()
    {
        var c = await NewCustomerAsync();
        var package = await NewPackageAsync(c.Id, 3000, 30, 20, 15);
        var origin = await NewStationAsync();
        var destination = await NewStationAsync();

        var response = await PostOrderAsync(c.Access, Body(package, origin, destination));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---------- 404 ----------

    [Fact]
    public async Task Create_returns_404_for_a_package_that_does_not_exist()
    {
        var c = await NewCustomerAsync();
        var origin = await NewStationAsync();
        var destination = await NewStationAsync();

        var response = await PostOrderAsync(c.Access, new
        {
            packageId = Guid.NewGuid(), originStationId = origin.Id, destinationStationId = destination.Id,
            recipientName = "Tran Thi Binh", recipientPhone = "0912345678"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_404_for_the_package_of_another_customer()
    {
        var owner = await NewCustomerAsync();
        var other = await NewCustomerAsync();
        var package = await NewPackageAsync(owner.Id);
        var origin = await NewStationAsync();
        var destination = await NewStationAsync();

        var response = await PostOrderAsync(other.Access, Body(package, origin, destination));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await _factory.WithDbAsync(async db => Assert.False(await db.DeliveryOrders.AnyAsync(o => o.PackageId == package.Id)));
    }

    [Theory]
    [InlineData("origin")]
    [InlineData("destination")]
    public async Task Create_returns_404_for_a_station_that_does_not_exist(string which)
    {
        var c = await NewCustomerAsync();
        var package = await NewPackageAsync(c.Id);
        var known = await NewStationAsync();

        var response = await PostOrderAsync(c.Access, new
        {
            packageId = package.Id,
            originStationId = which == "origin" ? Guid.NewGuid() : known.Id,
            destinationStationId = which == "destination" ? Guid.NewGuid() : known.Id,
            recipientName = "Tran Thi Binh", recipientPhone = "0912345678"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_404_for_a_delivery_address_that_does_not_exist_or_belongs_to_someone_else()
    {
        var c = await NewCustomerAsync();
        var other = await NewCustomerAsync();
        var origin = await NewStationAsync();
        var destination = await NewStationAsync();
        var foreignAddress = await NewAddressAsync(other.Id);

        var missing = await PostOrderAsync(c.Access, Body(await NewPackageAsync(c.Id), origin, destination, Guid.NewGuid()));
        var foreign = await PostOrderAsync(c.Access, Body(await NewPackageAsync(c.Id), origin, destination, foreignAddress.Id));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    // ---------- 409 ----------

    [Fact]
    public async Task Create_returns_409_when_the_package_is_already_linked_to_an_order()
    {
        var c = await NewCustomerAsync();
        var package = await NewPackageAsync(c.Id);
        var origin = await NewStationAsync();
        var destination = await NewStationAsync();

        Assert.Equal(HttpStatusCode.Created, (await PostOrderAsync(c.Access, Body(package, origin, destination))).StatusCode);
        var second = await PostOrderAsync(c.Access, Body(package, origin, destination));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        await _factory.WithDbAsync(async db => Assert.Equal(1, await db.DeliveryOrders.CountAsync(o => o.PackageId == package.Id)));
    }

    [Fact]
    public async Task Create_returns_409_for_a_package_whose_order_was_cancelled()
    {
        var c = await NewCustomerAsync();
        var package = await NewPackageAsync(c.Id);
        var origin = await NewStationAsync();
        var destination = await NewStationAsync();
        var first = await JsonAsync(await PostOrderAsync(c.Access, Body(package, origin, destination)));
        var orderId = Guid.Parse(first.GetProperty("id").GetString()!);
        await _factory.WithDbAsync(async db =>
        {
            (await db.DeliveryOrders.SingleAsync(o => o.Id == orderId)).Status = "CANCELLED";
            await db.SaveChangesAsync();
        });

        var again = await PostOrderAsync(c.Access, Body(package, origin, destination));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    // ---------- access ----------

    [Fact]
    public async Task Create_requires_a_token()
    {
        var response = await PostOrderAsync(null, new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_403_when_the_account_is_not_active()
    {
        var c = await NewCustomerAsync();
        var package = await NewPackageAsync(c.Id);
        var origin = await NewStationAsync();
        var destination = await NewStationAsync();
        await _factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == c.Id)).Status = "LOCKED";
            await db.SaveChangesAsync();
        });

        var response = await PostOrderAsync(c.Access, Body(package, origin, destination));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await _factory.WithDbAsync(async db => Assert.False(await db.DeliveryOrders.AnyAsync(o => o.CustomerId == c.Id)));
    }

    [Fact]
    public async Task Create_ignores_fields_the_client_must_not_send()
    {
        var c = await NewCustomerAsync();
        var other = await NewCustomerAsync();
        var package = await NewPackageAsync(c.Id);
        var origin = await NewStationAsync();
        var destination = await NewStationAsync();

        var response = await PostOrderAsync(c.Access, new
        {
            packageId = package.Id, originStationId = origin.Id, destinationStationId = destination.Id,
            recipientName = "Tran Thi Binh", recipientPhone = "0912345678",
            customerId = other.Id, status = "DELIVERED", trackingNumber = "DD-000000-0001", pickupSecureCode = "000000"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.Equal("PENDING", json.GetProperty("status").GetString());
        Assert.NotEqual("DD-000000-0001", json.GetProperty("trackingNumber").GetString());
        await _factory.WithDbAsync(async db =>
        {
            var order = await db.DeliveryOrders.SingleAsync(o => o.Id == Guid.Parse(json.GetProperty("id").GetString()!));
            Assert.Equal(c.Id, order.CustomerId);
        });
    }

    // ---------- GET /api/landing-stations ----------

    [Fact]
    public async Task Stations_requires_a_token()
    {
        var response = await _client.GetAsync("/api/landing-stations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Stations_can_be_filtered_by_status_and_are_sorted_by_name()
    {
        var c = await NewCustomerAsync();
        var tag = Guid.NewGuid().ToString("N")[..6];
        await NewStationAsync(name: $"{tag}-B");
        await NewStationAsync(name: $"{tag}-A");
        await NewStationAsync("MAINTENANCE", $"{tag}-C");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/landing-stations?status=operational");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", c.Access);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonAsync(response);
        var names = json.EnumerateArray().Select(s => s.GetProperty("name").GetString()!).ToList();
        Assert.All(json.EnumerateArray(), s => Assert.Equal("OPERATIONAL", s.GetProperty("status").GetString()));
        // Only this test's stations: the shared database also holds stations created by other tests.
        Assert.Equal(new[] { $"{tag}-A", $"{tag}-B" }, names.Where(n => n.StartsWith(tag)).ToArray());
    }
}
