using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class EtaPredictionPipelineTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static EtaPredictionService Service(AppDbContext db, TestEtaModel model) =>
        new(db, model, new OrderTransitionWriter(db), Options.Create(new DeliveryFlightOptions()), TimeProvider.System);

    [Fact]
    public async Task Stores_full_route_prediction_without_fabricating_arrival_or_confidence()
    {
        await factory.WithDbAsync(async db =>
        {
            var id = await EtaPipelineData.SeedAsync(db);
            var model = new TestEtaModel();
            Assert.Equal(EtaPredictionOutcome.Generated, await Service(db, model).RecalculateAsync(id));
            var p = await db.AiEtaPredictions.SingleAsync(p => p.OrderId == id);
            Assert.Equal(18, p.PredictedDurationMinutes);
            Assert.InRange(p.CalculatedDistanceKm, 10m, 15m);
            Assert.Null(p.ConfidenceScore);
            Assert.Null(p.WeatherCondition);
            Assert.Null((await db.DeliveryOrders.FindAsync(id))!.EstimatedDeliveryTime);
            Assert.Equal(64, p.InputFingerprint!.Length);
            Assert.Equal("test-model-v1", p.ModelVersion);
            Assert.Equal(450m, model.LastInput!.WeightGram);
            Assert.Null(model.LastInput.WeatherCondition);
            var created = p.CreatedAt;
            Assert.Equal(EtaPredictionOutcome.Unchanged, await Service(db, model).RecalculateAsync(id));
            Assert.Equal(1, model.Calls);
            Assert.Equal(created, p.CreatedAt);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(1441)]
    public async Task Invalid_model_duration_writes_nothing(int duration)
    {
        await factory.WithDbAsync(async db =>
        {
            var id = await EtaPipelineData.SeedAsync(db);
            var model = new TestEtaModel { Predict = (_, _) => Task.FromResult(duration) };
            Assert.Equal(EtaPredictionOutcome.Unavailable, await Service(db, model).RecalculateAsync(id));
            Assert.False(await db.AiEtaPredictions.AnyAsync(p => p.OrderId == id));
        });
    }

    [Fact]
    public async Task Unconfigured_or_failed_model_does_not_write_or_freshen_prediction()
    {
        await factory.WithDbAsync(async db =>
        {
            var id = await EtaPipelineData.SeedAsync(db);
            var model = new TestEtaModel { IsConfigured = false };
            Assert.Equal(EtaPredictionOutcome.Unavailable, await Service(db, model).RecalculateAsync(id));
            Assert.Equal(0, model.Calls);
            model.IsConfigured = true;
            model.Predict = (_, _) => throw new HttpRequestException("Provider unavailable");
            Assert.Equal(EtaPredictionOutcome.Unavailable, await Service(db, model).RecalculateAsync(id));
            Assert.False(await db.AiEtaPredictions.AnyAsync(p => p.OrderId == id));
        });
    }

    [Theory]
    [InlineData("IN_TRANSIT", false)]
    [InlineData("ARRIVED", false)]
    [InlineData("DELIVERED", false)]
    [InlineData("FAILED", false)]
    [InlineData("CANCELLED", false)]
    [InlineData("APPROVED", true)]
    public async Task Ineligible_or_scheduled_orders_never_invoke_model(string status, bool scheduled)
    {
        await factory.WithDbAsync(async db =>
        {
            var id = await EtaPipelineData.SeedAsync(db, status, scheduled);
            var model = new TestEtaModel();
            Assert.Equal(EtaPredictionOutcome.Ineligible, await Service(db, model).RecalculateAsync(id));
            Assert.Equal(0, model.Calls);
        });
    }

    [Fact]
    public async Task Changed_weight_during_generation_discards_result()
    {
        Guid id = default;
        await factory.WithDbAsync(async db => id = await EtaPipelineData.SeedAsync(db));
        var model = new TestEtaModel { Predict = async (_, _) =>
        {
            await factory.WithDbAsync(async other =>
            {
                var order = await other.DeliveryOrders.Include(o => o.Package).SingleAsync(o => o.Id == id);
                order.Package.WeightGram = 900;
                await other.SaveChangesAsync();
            });
            return 18;
        }};
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(EtaPredictionOutcome.Superseded, await Service(db, model).RecalculateAsync(id));
            Assert.False(await db.AiEtaPredictions.AnyAsync(p => p.OrderId == id));
        });
    }

    [Fact]
    public async Task Scheduling_during_generation_keeps_existing_operational_state()
    {
        Guid id = default;
        var departure = DateTime.UtcNow.AddHours(1);
        await factory.WithDbAsync(async db => id = await EtaPipelineData.SeedAsync(db));
        var model = new TestEtaModel { Predict = async (_, _) =>
        {
            await factory.WithDbAsync(async other =>
            {
                var order = await other.DeliveryOrders.SingleAsync(o => o.Id == id);
                order.Status = "APPROVED";
                order.ScheduledDepartureTime = departure;
                order.EstimatedDeliveryTime = departure.AddMinutes(25);
                await other.SaveChangesAsync();
            });
            return 18;
        }};
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(EtaPredictionOutcome.Superseded, await Service(db, model).RecalculateAsync(id));
            var order = await db.DeliveryOrders.AsNoTracking().SingleAsync(o => o.Id == id);
            Assert.Equal(departure.AddMinutes(25), order.EstimatedDeliveryTime);
            Assert.False(await db.AiEtaPredictions.AnyAsync(p => p.OrderId == id));
        });
    }

    [Fact]
    public async Task Invalid_legacy_coordinates_are_not_sent_to_model()
    {
        await factory.WithDbAsync(async db =>
        {
            var id = await EtaPipelineData.SeedAsync(db);
            var order = await db.DeliveryOrders.Include(o => o.OriginStation).SingleAsync(o => o.Id == id);
            order.OriginStation.Latitude = 91;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var model = new TestEtaModel();
            Assert.Equal(EtaPredictionOutcome.Ineligible, await Service(db, model).RecalculateAsync(id));
            Assert.Equal(0, model.Calls);
        });
    }

    [Fact]
    public async Task Scheduling_does_not_consume_stamped_prediction_after_package_input_changes()
    {
        await factory.WithDbAsync(async db =>
        {
            var id = await EtaPipelineData.SeedAsync(db);
            Assert.Equal(EtaPredictionOutcome.Generated, await Service(db, new TestEtaModel()).RecalculateAsync(id));
            var order = await db.DeliveryOrders.Include(o => o.Package).SingleAsync(o => o.Id == id);
            order.Package.WeightGram = 900;
            await db.SaveChangesAsync();
            var route = await new DeliveryRouteEstimate(db, Options.Create(new DeliveryFlightOptions())).GetAsync(order);
            Assert.Equal(16, route.FlightMinutes);
            Assert.Equal(18, (await db.AiEtaPredictions.SingleAsync(p => p.OrderId == id)).PredictedDurationMinutes);
        });
    }

    [Fact]
    public async Task Slower_competing_result_cannot_overwrite_committed_prediction()
    {
        Guid id = default;
        await factory.WithDbAsync(async db => id = await EtaPipelineData.SeedAsync(db));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        EtaPredictionOutcome slowResult = default;
        var slow = new TestEtaModel { Predict = (_, _) => { entered.SetResult(); return release.Task; } };
        var first = factory.WithDbAsync(async db => slowResult = await Service(db, slow).RecalculateAsync(id));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await factory.WithDbAsync(async db => Assert.Equal(EtaPredictionOutcome.Generated,
                await Service(db, new TestEtaModel { Predict = (_, _) => Task.FromResult(25) }).RecalculateAsync(id)));
        }
        finally { release.TrySetResult(18); }
        await first;
        // A deadline can discard the old result before the competing write finishes under load.
        Assert.Contains(slowResult, new[] { EtaPredictionOutcome.Superseded, EtaPredictionOutcome.Unavailable });
        await factory.WithDbAsync(async db => Assert.Equal(25,
            (await db.AiEtaPredictions.SingleAsync(p => p.OrderId == id)).PredictedDurationMinutes));
    }
}

internal sealed class TestEtaModel : IEtaModelGateway
{
    public bool IsConfigured { get; set; } = true;
    public string ModelVersion => "test-model-v1";
    public int Calls { get; private set; }
    public EtaModelInput? LastInput { get; private set; }
    public Func<EtaModelInput, CancellationToken, Task<int>> Predict { get; set; } = (_, _) => Task.FromResult(18);
    public Task<int> PredictDurationAsync(EtaModelInput input, CancellationToken ct)
    {
        Calls++;
        LastInput = input;
        return Predict(input, ct);
    }
}

internal static class EtaPipelineData
{
    public static async Task<Guid> SeedAsync(AppDbContext db, string status = "PENDING", bool scheduled = false)
    {
        var unique = Guid.NewGuid().ToString("N");
        var owner = new User { RoleId = (await db.Roles.SingleAsync(r => r.Code == "CUSTOMER")).Id,
            Email = $"eta-{unique}@example.com", FullName = "Private customer", PhoneNumber = unique[..15] };
        var order = new DeliveryOrder { Customer = owner, TrackingNumber = $"ETA-{unique}", Status = status,
            PickupSecureCode = "654321", RecipientName = "Private recipient", RecipientPhone = "0909999999",
            Package = new Package { Sender = owner, PackageName = "Private contents", WeightGram = 450 },
            OriginStation = new LandingStation { StationCode = $"O-{unique[..8]}", Name = "Origin", Latitude = 10, Longitude = 106 },
            DestinationStation = new LandingStation { StationCode = $"D-{unique[..8]}", Name = "Destination", Latitude = 10.1m, Longitude = 106 },
            ScheduledDepartureTime = scheduled ? DateTime.UtcNow.AddHours(1) : null };
        db.Add(order);
        await db.SaveChangesAsync();
        var id = order.Id;
        db.ChangeTracker.Clear();
        return id;
    }
}
