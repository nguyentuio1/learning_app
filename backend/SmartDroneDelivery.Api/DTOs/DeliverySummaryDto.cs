namespace SmartDroneDelivery.Api.DTOs;

public record DeliverySummaryDto(Guid OrderId, string Outcome, string? Text, DateTime? GeneratedAt);
