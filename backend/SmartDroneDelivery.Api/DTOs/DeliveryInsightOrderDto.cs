namespace SmartDroneDelivery.Api.DTOs;

// Deliberately excludes customer identifiers, package/contact data and pickup codes.
public record DeliveryInsightOrderDto(Guid Id, string TrackingNumber, string Status,
    string OriginStationName, string DestinationStationName, DateTime CreatedAt);
