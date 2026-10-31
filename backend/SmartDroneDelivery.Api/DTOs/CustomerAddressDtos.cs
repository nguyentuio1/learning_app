using System.ComponentModel.DataAnnotations;

namespace SmartDroneDelivery.Api.DTOs;

public record CustomerAddressDto(
    Guid Id,
    Guid UserId,
    string Label,
    string ContactName,
    string ContactPhone,
    string AddressLine,
    decimal? Latitude,
    decimal? Longitude,
    bool IsDefault,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateCustomerAddressRequest(
    [Required, StringLength(50)] string Label,
    [Required, StringLength(150)] string ContactName,
    [Required, Phone, StringLength(20)] string ContactPhone,
    [Required] string AddressLine,
    [Range(-90.0, 90.0)] decimal? Latitude,
    [Range(-180.0, 180.0)] decimal? Longitude,
    bool IsDefault = false);

public record UpdateCustomerAddressRequest(
    [Required, StringLength(50)] string Label,
    [Required, StringLength(150)] string ContactName,
    [Required, Phone, StringLength(20)] string ContactPhone,
    [Required] string AddressLine,
    [Range(-90.0, 90.0)] decimal? Latitude,
    [Range(-180.0, 180.0)] decimal? Longitude,
    bool IsDefault = false);
