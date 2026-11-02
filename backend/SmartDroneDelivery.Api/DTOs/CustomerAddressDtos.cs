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
    [Required(ErrorMessage = "Label is required."), StringLength(50, ErrorMessage = "Label must be at most 50 characters.")] string Label,
    [Required(ErrorMessage = "Contact name is required."), StringLength(150, ErrorMessage = "Contact name must be at most 150 characters.")] string ContactName,
    [Required(ErrorMessage = "Contact phone is required."), RegularExpression(@"^\d{9,11}$", ErrorMessage = "Contact phone must be 9 to 11 digits.")] string ContactPhone,
    [Required(ErrorMessage = "Address line is required.")] string AddressLine,
    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")] decimal? Latitude,
    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")] decimal? Longitude,
    bool IsDefault = false);

public record UpdateCustomerAddressRequest(
    [Required(ErrorMessage = "Label is required."), StringLength(50, ErrorMessage = "Label must be at most 50 characters.")] string Label,
    [Required(ErrorMessage = "Contact name is required."), StringLength(150, ErrorMessage = "Contact name must be at most 150 characters.")] string ContactName,
    [Required(ErrorMessage = "Contact phone is required."), RegularExpression(@"^\d{9,11}$", ErrorMessage = "Contact phone must be 9 to 11 digits.")] string ContactPhone,
    [Required(ErrorMessage = "Address line is required.")] string AddressLine,
    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")] decimal? Latitude,
    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")] decimal? Longitude,
    bool IsDefault = false);
