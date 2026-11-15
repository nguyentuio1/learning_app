using System.ComponentModel.DataAnnotations;

namespace SmartDroneDelivery.Api.DTOs;

public class PendingOrderFilterQuery
{
    public Guid? OriginStationId { get; set; }
    public Guid? DestinationStationId { get; set; }
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public record ReviewStationDto(Guid Id, string StationCode, string Name);

public record PendingOrderDto(
    Guid OrderId, string TrackingNumber, string CustomerName, string CustomerPhone,
    string PackageName, decimal WeightGram, decimal LengthCm, decimal WidthCm, decimal HeightCm, bool IsFragile,
    ReviewStationDto OriginStation, ReviewStationDto DestinationStation, DateTime CreatedAt);

public record RejectOrderRequest(string? Reason);

public record OrderDecisionDto(Guid OrderId, string TrackingNumber, string Status, DateTime UpdatedAt);
