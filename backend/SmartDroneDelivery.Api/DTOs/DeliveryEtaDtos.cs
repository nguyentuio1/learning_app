using System.Text;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.DTOs;

public record AiEtaPredictionDto(decimal CalculatedDistanceKm, int PredictedDurationMinutes,
    string? WeatherCondition, decimal? ConfidenceScore, DateTime CreatedAt)
{
    // This projection makes no freshness claim and never changes the scheduling input.
    public static AiEtaPredictionDto? From(AiEtaPrediction? prediction)
    {
        if (prediction is null || prediction.CalculatedDistanceKm is <= 0 or > 9999.99m
            || prediction.PredictedDurationMinutes <= 0
            || prediction.ConfidenceScore is < 0 or > 1
            || prediction.WeatherCondition?.EnumerateRunes().Count() > 50)
            return null;
        return new(prediction.CalculatedDistanceKm, prediction.PredictedDurationMinutes,
            prediction.WeatherCondition, prediction.ConfidenceScore,
            DateTime.SpecifyKind(prediction.CreatedAt, DateTimeKind.Utc));
    }
}

public record DeliveryEtaDto(Guid OrderId, string Status, DateTime? EstimatedDeliveryTime,
    DateTime? ActualDeliveryTime, AiEtaPredictionDto? AiEtaPrediction);
