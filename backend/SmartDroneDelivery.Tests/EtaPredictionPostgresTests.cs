using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class EtaPredictionPostgresTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static EtaPredictionService Service(AppDbContext db, TestEtaModel model) =>
        new(db, model, new OrderTransitionWriter(db), Options.Create(new DeliveryFlightOptions()), TimeProvider.System);

    [PostgresFact]
    public async Task Model_runs_outside_transaction_and_saved_prediction_is_migration_compatible()
    {
        await factory.WithDbAsync(async db =>
        {
            var id = await EtaPipelineData.SeedAsync(db);
            var model = new TestEtaModel { Predict = (_, _) =>
            {
                Assert.Null(db.Database.CurrentTransaction);
                return Task.FromResult(18);
            }};
            Assert.Equal(EtaPredictionOutcome.Generated, await Service(db, model).RecalculateAsync(id));
            db.ChangeTracker.Clear();
            var row = await db.AiEtaPredictions.SingleAsync(p => p.OrderId == id);
            Assert.Equal(64, row.InputFingerprint!.Length);
            Assert.Equal("test-model-v1", row.ModelVersion);
            Assert.Equal(18, row.PredictedDurationMinutes);
            Assert.Null(row.WeatherCondition);
            Assert.Null(row.ConfidenceScore);
            Assert.Equal(EtaPredictionOutcome.Unchanged, await Service(db, model).RecalculateAsync(id));
            Assert.Equal(1, model.Calls);
            var route = await new DeliveryRouteEstimate(db, Options.Create(new DeliveryFlightOptions()))
                .GetAsync(await db.DeliveryOrders.SingleAsync(o => o.Id == id));
            Assert.Equal(18, route.FlightMinutes);
        });
    }

    [PostgresFact]
    public async Task Failure_after_insert_rolls_back_prediction_and_leaves_order_untouched()
    {
        Guid id = default;
        await factory.WithDbAsync(async db => id = await EtaPipelineData.SeedAsync(db));
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(factory.DatabaseConnectionString).AddInterceptors(new FailAfterPredictionSave()).Options))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, new TestEtaModel()).RecalculateAsync(id));
        }
        await factory.WithDbAsync(async db =>
        {
            Assert.False(await db.AiEtaPredictions.AnyAsync(p => p.OrderId == id));
            var order = await db.DeliveryOrders.SingleAsync(o => o.Id == id);
            Assert.Equal("PENDING", order.Status);
            Assert.Null(order.EstimatedDeliveryTime);
            Assert.Null(order.ScheduledDepartureTime);
        });
    }

    [PostgresFact]
    public async Task Competing_initial_predictions_have_one_winner_and_cannot_overwrite_it()
    {
        Guid id = default;
        await factory.WithDbAsync(async db => id = await EtaPipelineData.SeedAsync(db));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = new TestEtaModel { Predict = (_, _) => { entered.SetResult(); return release.Task; } };
        EtaPredictionOutcome result = default;
        var first = factory.WithDbAsync(async db => result = await Service(db, slow).RecalculateAsync(id));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await factory.WithDbAsync(async db => Assert.Equal(EtaPredictionOutcome.Generated,
                await Service(db, new TestEtaModel { Predict = (_, _) => Task.FromResult(25) }).RecalculateAsync(id)));
        }
        finally { release.TrySetResult(18); }
        await first;
        Assert.Contains(result, new[] { EtaPredictionOutcome.Superseded, EtaPredictionOutcome.Unavailable });
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.AiEtaPredictions.CountAsync(p => p.OrderId == id));
            Assert.Equal(25, (await db.AiEtaPredictions.SingleAsync(p => p.OrderId == id)).PredictedDurationMinutes);
        });
    }

    [PostgresFact]
    public async Task Station_input_change_without_order_timestamp_change_invalidates_candidate()
    {
        Guid id = default;
        await factory.WithDbAsync(async db => id = await EtaPipelineData.SeedAsync(db));
        var model = new TestEtaModel { Predict = async (_, _) =>
        {
            await factory.WithDbAsync(async other =>
            {
                var station = await other.DeliveryOrders.Where(o => o.Id == id).Select(o => o.DestinationStationId).SingleAsync();
                await other.LandingStations.Where(s => s.Id == station).ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Latitude, 10.2m));
            });
            return 18;
        }};
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(EtaPredictionOutcome.Superseded, await Service(db, model).RecalculateAsync(id));
            Assert.False(await db.AiEtaPredictions.AnyAsync(p => p.OrderId == id));
        });
    }

    private sealed class FailAfterPredictionSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default) =>
            throw new InvalidOperationException("Injected post-save failure");
    }
}
