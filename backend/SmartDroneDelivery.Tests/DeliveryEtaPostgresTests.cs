using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class DeliveryEtaPostgresTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [PostgresFact]
    public async Task Relational_projection_matches_customer_detail_and_remains_read_only()
    {
        await factory.WithDbAsync(async db =>
        {
            var missionId = await TelemetryTestData.SeedMissionAsync(db);
            var mission = await db.FlightMissions.Include(m => m.Order).SingleAsync(m => m.Id == missionId);
            var created = new DateTime(2026, 11, 16, 7, 55, 0, DateTimeKind.Utc);
            db.AiEtaPredictions.Add(new AiEtaPrediction { OrderId = mission.OrderId,
                CalculatedDistanceKm = 12.35m, PredictedDurationMinutes = 18,
                ConfidenceScore = 0.85m, WeatherCondition = "CLEAR", CreatedAt = created });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var eta = await new DeliveryEtaQueryService(db).GetAsync(mission.Order.CustomerId, mission.OrderId);
            var detail = await new DeliveryOrderQueryService(db).GetByIdAsync(mission.OrderId, mission.Order.CustomerId);
            Assert.NotNull(eta.AiEtaPrediction);
            Assert.Equal(eta.AiEtaPrediction, detail.AiEtaPrediction);
            Assert.Equal(12.35m, eta.AiEtaPrediction.CalculatedDistanceKm);
            Assert.Equal(18, eta.AiEtaPrediction.PredictedDurationMinutes);
            Assert.Equal(0.85m, eta.AiEtaPrediction.ConfidenceScore);
            Assert.Equal("CLEAR", eta.AiEtaPrediction.WeatherCondition);
            Assert.Equal(created, eta.AiEtaPrediction.CreatedAt);
            Assert.Null(eta.EstimatedDeliveryTime);
            Assert.False(db.ChangeTracker.HasChanges());
            Assert.Empty(db.ChangeTracker.Entries());
        });
    }
}
