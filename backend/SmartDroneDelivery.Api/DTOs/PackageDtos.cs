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
    [Required(ErrorMessage = "Package name is required."), StringLength(200, MinimumLength = 1, ErrorMessage = "Package name must be 1 to 200 characters.")] string PackageName,
    decimal WeightGram,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    bool IsFragile = false,
    string? PackageImageUrl = null);

public record UpdatePackageRequest(
    [Required(ErrorMessage = "Package name is required."), StringLength(200, MinimumLength = 1, ErrorMessage = "Package name must be 1 to 200 characters.")] string PackageName,
    decimal WeightGram,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    bool IsFragile = false,
    string? PackageImageUrl = null);

public record PackageImageResponse(string ImageUrl);
