using System.Text.Json.Serialization;

namespace SmartDroneDelivery.Api.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ConfirmDeliveryRequest
{
    public string? PickupSecureCode { get; set; }
}
