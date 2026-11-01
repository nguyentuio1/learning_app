using System.ComponentModel.DataAnnotations;

namespace SmartDroneDelivery.Api.DTOs;

public record PackageDto(
    Guid Id,
    Guid SenderId,
    string PackageName,
    decimal WeightGram,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    string? PackageImageUrl,
    bool IsFragile,
    DateTime CreatedAt);

public record CreatePackageRequest(
    [Required, StringLength(200, MinimumLength = 1)] string PackageName,
    [Range(1.0, 5000.0, ErrorMessage = "Weight must be between 1g and 5000g.")] decimal WeightGram,
    [Range(1.0, 100.0, ErrorMessage = "Length must be between 1cm and 100cm.")] decimal LengthCm,
    [Range(1.0, 100.0, ErrorMessage = "Width must be between 1cm and 100cm.")] decimal WidthCm,
    [Range(1.0, 100.0, ErrorMessage = "Height must be between 1cm and 100cm.")] decimal HeightCm,
    bool IsFragile = false,
    string? PackageImageUrl = null);

public record UpdatePackageRequest(
    [Required, StringLength(200, MinimumLength = 1)] string PackageName,
    [Range(1.0, 5000.0, ErrorMessage = "Weight must be between 1g and 5000g.")] decimal WeightGram,
    [Range(1.0, 100.0, ErrorMessage = "Length must be between 1cm and 100cm.")] decimal LengthCm,
    [Range(1.0, 100.0, ErrorMessage = "Width must be between 1cm and 100cm.")] decimal WidthCm,
    [Range(1.0, 100.0, ErrorMessage = "Height must be between 1cm and 100cm.")] decimal HeightCm,
    bool IsFragile = false,
    string? PackageImageUrl = null);

public record PackageImageResponse(string ImageUrl);
