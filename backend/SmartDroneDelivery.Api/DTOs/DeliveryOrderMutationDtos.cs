using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SmartDroneDelivery.Api.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateDeliveryOrderRequest(
    [Required(ErrorMessage = "Recipient name is required."), StringLength(150, MinimumLength = 1, ErrorMessage = "Recipient name must be 1 to 150 characters.")] string? RecipientName,
    [Required(ErrorMessage = "Recipient phone is required."), RegularExpression(@"^\d{9,11}$", ErrorMessage = "Recipient phone must be 9 to 11 digits.")] string? RecipientPhone,
    Guid? DeliveryAddressId = null,
    [Required(ErrorMessage = "Destination station ID is required.")] Guid? DestinationStationId = null);

public record CancelDeliveryOrderRequest(
    [StringLength(500, ErrorMessage = "Cancellation reason must be at most 500 characters.")] string? CancellationReason = null);
