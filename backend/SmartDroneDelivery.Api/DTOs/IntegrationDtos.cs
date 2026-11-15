using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SmartDroneDelivery.Api.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CreateIntegrationKeyRequest(Guid CustomerId, string? Name, string[]? Scopes, DateTime? ExpiresAt);
public record IntegrationKeyMetadata(Guid Id, string Name, Guid CustomerId, string[] Scopes, DateTime CreatedAt, DateTime ExpiresAt, DateTime? RevokedAt);
public record CreatedIntegrationKey(Guid Id, string Name, Guid CustomerId, string[] Scopes, DateTime CreatedAt, DateTime ExpiresAt, DateTime? RevokedAt, string ApiKey);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CreateExternalOrderRequest(Guid PackageId, Guid OriginStationId, Guid DestinationStationId, Guid? DeliveryAddressId,
    [Required][StringLength(150)] string RecipientName,
    [Required][RegularExpression(@"^\d{9,11}$", ErrorMessage = "Phone number must contain 9 to 11 digits.")] string RecipientPhone)
{
    public CreateDeliveryOrderRequest ToInternal() => new(PackageId,OriginStationId,DestinationStationId,DeliveryAddressId,RecipientName,RecipientPhone);
}
public record CreatedExternalOrder(Guid Id, string TrackingNumber, string Status, string PickupSecureCode, DateTime CreatedAt);
public record ExternalOrder(Guid Id, string TrackingNumber, string Status, Guid PackageId, Guid OriginStationId, Guid DestinationStationId,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime? EstimatedDeliveryTime, DateTime? ActualDeliveryTime);
