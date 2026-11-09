using System.Text.Json.Serialization;

namespace SmartDroneDelivery.Api.DTOs;

public record StationSlotDto(
    Guid Id,
    Guid StationId,
    int SlotNumber,
    string Status,
    DateTime LastUpdated,
    Guid? CurrentOrderId,
    Guid? CurrentMissionId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class AddStationSlotRequest
{
    public int? SlotNumber { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class UpdateStationSlotStatusRequest
{
    public string? TargetStatus { get; set; }
    public string? Reason { get; set; }
}
