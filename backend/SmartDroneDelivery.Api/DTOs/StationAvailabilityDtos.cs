namespace SmartDroneDelivery.Api.DTOs;

public record StationAvailabilityDto(
    Guid StationId,
    string StationStatus,
    int TotalSlots,
    int EmptySlots,
    int OccupiedSlots,
    int ReservedSlots,
    int ErrorSlots,
    bool IsAvailableForArrival,
    bool IsAvailableForDeparture,
    DateTime CheckedAt);
