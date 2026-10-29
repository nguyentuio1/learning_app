namespace SmartDroneDelivery.Api.Entities;

public class AiEtaPrediction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public decimal CalculatedDistanceKm { get; set; }
    public int PredictedDurationMinutes { get; set; }
    public string? WeatherCondition { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DeliveryOrder Order { get; set; } = null!;
}
