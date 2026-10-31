using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public interface IPackageService
{
    Task<IReadOnlyList<PackageDto>> ListAsync(Guid userId);
    Task<PackageDto> GetByIdAsync(Guid packageId, Guid userId, string userRole);
    Task<PackageDto> CreateAsync(Guid userId, CreatePackageRequest request);
    Task<PackageDto> UpdateAsync(Guid packageId, Guid userId, string userRole, UpdatePackageRequest request);
    Task<PackageImageResponse> UploadImageAsync(Guid packageId, Guid userId, string userRole, IFormFile? file);
}

public class PackageService : IPackageService
{
    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp"
    };
    private const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB

    private readonly AppDbContext _db;
    private readonly IPackageStorage _storage;

    public PackageService(AppDbContext db, IPackageStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IReadOnlyList<PackageDto>> ListAsync(Guid userId)
    {
        return await _db.Packages
            .AsNoTracking()
            .Where(p => p.SenderId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => ToDto(p))
            .ToListAsync();
    }

    public async Task<PackageDto> GetByIdAsync(Guid packageId, Guid userId, string userRole)
    {
        var package = await _db.Packages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == packageId);

        if (package == null)
            throw new ApiException(StatusCodes.Status404NotFound, "Package not found.");

        if (userRole == AppRoles.Customer && package.SenderId != userId)
            throw new ApiException(StatusCodes.Status403Forbidden, "You do not have permission to view this package.");

        return ToDto(package);
    }

    public async Task<PackageDto> CreateAsync(Guid userId, CreatePackageRequest request)
    {
        ValidateDimensions(request.WeightGram, request.LengthCm, request.WidthCm, request.HeightCm);

        var package = new Package
        {
            Id = Guid.NewGuid(),
            SenderId = userId,
            PackageName = request.PackageName.Trim(),
            WeightGram = request.WeightGram,
            LengthCm = request.LengthCm,
            WidthCm = request.WidthCm,
            HeightCm = request.HeightCm,
            IsFragile = request.IsFragile,
            PackageImageUrl = request.PackageImageUrl,
            CreatedAt = DateTime.UtcNow
        };

        _db.Packages.Add(package);
        await _db.SaveChangesAsync();

        return ToDto(package);
    }

    public async Task<PackageDto> UpdateAsync(Guid packageId, Guid userId, string userRole, UpdatePackageRequest request)
    {
        ValidateDimensions(request.WeightGram, request.LengthCm, request.WidthCm, request.HeightCm);

        var package = await _db.Packages
            .Include(p => p.DeliveryOrder)
            .FirstOrDefaultAsync(p => p.Id == packageId);

        if (package == null)
            throw new ApiException(StatusCodes.Status404NotFound, "Package not found.");

        if (userRole == AppRoles.Customer && package.SenderId != userId)
            throw new ApiException(StatusCodes.Status403Forbidden, "You do not have permission to modify this package.");

        // If package is linked to an order that is already in progress, prevent modifications
        if (package.DeliveryOrder != null && package.DeliveryOrder.Status != "PENDING" && package.DeliveryOrder.Status != "CANCELLED")
        {
            throw new ApiException(StatusCodes.Status409Conflict,
                $"Cannot modify package because associated delivery order is in status '{package.DeliveryOrder.Status}'.");
        }

        package.PackageName = request.PackageName.Trim();
        package.WeightGram = request.WeightGram;
        package.LengthCm = request.LengthCm;
        package.WidthCm = request.WidthCm;
        package.HeightCm = request.HeightCm;
        package.IsFragile = request.IsFragile;
        if (!string.IsNullOrWhiteSpace(request.PackageImageUrl))
        {
            package.PackageImageUrl = request.PackageImageUrl;
        }

        await _db.SaveChangesAsync();
        return ToDto(package);
    }

    public async Task<PackageImageResponse> UploadImageAsync(Guid packageId, Guid userId, string userRole, IFormFile? file)
    {
        if (file == null || file.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "No file was uploaded.");

        if (file.Length > MaxImageBytes)
            throw new ApiException(StatusCodes.Status400BadRequest, "Image file size exceeds the 5 MB limit.");

        if (!AllowedImageTypes.Contains(file.ContentType))
            throw new ApiException(StatusCodes.Status400BadRequest, "Unsupported image type. Use JPEG, PNG, or WebP.");

        var package = await _db.Packages.FirstOrDefaultAsync(p => p.Id == packageId);
        if (package == null)
            throw new ApiException(StatusCodes.Status404NotFound, "Package not found.");

        if (userRole == AppRoles.Customer && package.SenderId != userId)
            throw new ApiException(StatusCodes.Status403Forbidden, "You do not have permission to update this package.");

        var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(ext))
            ext = "jpg";

        await using var stream = file.OpenReadStream();
        var imageUrl = await _storage.UploadAsync(package.Id, stream, file.Length, file.ContentType, ext);

        // Remove old image if different
        if (!string.IsNullOrEmpty(package.PackageImageUrl) && package.PackageImageUrl != imageUrl)
        {
            await _storage.DeleteAsync(package.PackageImageUrl);
        }

        package.PackageImageUrl = imageUrl;
        await _db.SaveChangesAsync();

        return new PackageImageResponse(imageUrl);
    }

    private static void ValidateDimensions(decimal weightGram, decimal lengthCm, decimal widthCm, decimal heightCm)
    {
        if (weightGram <= 0 || weightGram > 5000)
            throw new ApiException(StatusCodes.Status400BadRequest, "Weight must be between 1g and 5000g.");

        if (lengthCm <= 0 || lengthCm > 100)
            throw new ApiException(StatusCodes.Status400BadRequest, "Length must be between 1cm and 100cm.");

        if (widthCm <= 0 || widthCm > 100)
            throw new ApiException(StatusCodes.Status400BadRequest, "Width must be between 1cm and 100cm.");

        if (heightCm <= 0 || heightCm > 100)
            throw new ApiException(StatusCodes.Status400BadRequest, "Height must be between 1cm and 100cm.");
    }

    private static PackageDto ToDto(Package p) => new(
        p.Id,
        p.SenderId,
        p.PackageName,
        p.WeightGram,
        p.LengthCm,
        p.WidthCm,
        p.HeightCm,
        p.PackageImageUrl,
        p.IsFragile,
        p.CreatedAt);
}
