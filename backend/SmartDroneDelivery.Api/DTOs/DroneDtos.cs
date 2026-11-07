using System.Text.Json.Serialization;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.DTOs;

public record DroneDto(
    Guid Id,
    string SerialNumber,
    string ModelName,
    decimal MaxPayloadGram,
    decimal MaxFlightRangeKm,
    int BatteryCapacityMah,
    string Status,
    Guid? CurrentStationId,
    string? CurrentStationName,
    DateTime CreatedAt)
{
    public static DroneDto From(Drone d)
    {
        var isFlying = d.Status == "FLYING";
        return new(
            d.Id,
            d.SerialNumber,
            d.ModelName,
            d.MaxPayloadGram,
            d.MaxFlightRangeKm,
            d.BatteryCapacityMah,
            d.Status,
            isFlying ? null : d.CurrentStationId,
            isFlying ? null : d.CurrentStation?.Name,
            DateTime.SpecifyKind(d.CreatedAt, DateTimeKind.Utc));
    }
}

public record DroneDetailDto(
    Guid Id,
    string SerialNumber,
    string ModelName,
    decimal MaxPayloadGram,
    decimal MaxFlightRangeKm,
    int BatteryCapacityMah,
    string Status,
    Guid? CurrentStationId,
    string? CurrentStationName,
    DateTime CreatedAt,
    Guid? ActiveMissionId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateDroneRequest
{
    public string? SerialNumber { get; set; }
    public string? ModelName { get; set; }
    public decimal? MaxPayloadGram { get; set; }
    public decimal? MaxFlightRangeKm { get; set; }
    public int? BatteryCapacityMah { get; set; }
    public Guid? CurrentStationId { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class UpdateDroneRequest
{
    public string? ModelName { get; set; }
    public decimal? MaxPayloadGram { get; set; }
    public decimal? MaxFlightRangeKm { get; set; }
    public int? BatteryCapacityMah { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class DroneMaintenanceRequest
{
    public string? Action { get; set; }
    public string? ExpectedStatus { get; set; }
    public string? Reason { get; set; }
    public bool? InspectionPassed { get; set; }
    public bool? ReadyForService { get; set; }
}
