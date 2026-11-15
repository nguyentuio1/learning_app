using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public interface IPackageService
{
    Task<IReadOnlyList<PackageDto>> ListAsync(Guid userId, bool? available = null);
    Task<PackageDto> GetByIdAsync(Guid packageId, Guid userId, string userRole);
    Task<PackageDto> CreateAsync(Guid userId, CreatePackageRequest request);
    Task<PackageDto> UpdateAsync(Guid packageId, Guid userId, string userRole, UpdatePackageRequest request);
    Task DeleteAsync(Guid packageId, Guid userId, string userRole);
    Task<PackageImageResponse> UploadImageAsync(Guid packageId, Guid userId, string userRole, IFormFile? file);
}

public class PackageService : IPackageService
{
    private const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB

    private readonly AppDbContext _db;
    private readonly IPackageStorage _storage;
    private readonly IOrderTransitionWriter _transitions;

    public PackageService(AppDbContext db, IPackageStorage storage, IOrderTransitionWriter? transitions = null)
    {
        _db = db;
        _storage = storage;
        _transitions = transitions ?? new OrderTransitionWriter(db);
    }

    public async Task<IReadOnlyList<PackageDto>> ListAsync(Guid userId, bool? available = null)
    {
        await EnsureActiveUserAsync(userId);

        var query = _db.Packages
            .AsNoTracking()
            .Where(p => p.SenderId == userId);

        if (available == true)
        {
            // BR-CO-08: one package, one order, never reused even if the order is cancelled
            query = query.Where(p => p.DeliveryOrder == null);
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => ToDto(p))
            .ToListAsync();
    }

    public async Task<PackageDto> GetByIdAsync(Guid packageId, Guid userId, string userRole)
    {
        await EnsureActiveUserAsync(userId);

        var package = await _db.Packages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == packageId);

        if (package == null || (userRole == AppRoles.Customer && package.SenderId != userId))
            throw new ApiException(StatusCodes.Status404NotFound, "Package not found.");

        return ToDto(package);
    }

    public async Task<PackageDto> CreateAsync(Guid userId, CreatePackageRequest request)
    {
        await EnsureActiveUserAsync(userId);
        ValidatePackageFields(request.PackageName, request.WeightGram, request.LengthCm, request.WidthCm, request.HeightCm, request.PackageImageUrl);

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
            PackageImageUrl = string.IsNullOrWhiteSpace(request.PackageImageUrl) ? null : request.PackageImageUrl.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Packages.Add(package);
        await _db.SaveChangesAsync();

        return ToDto(package);
    }

    public async Task<PackageDto> UpdateAsync(Guid packageId, Guid userId, string userRole, UpdatePackageRequest request)
    {
        using var operation = await _transitions.EnterOperationAsync();
        await EnsureActiveUserAsync(userId);
        ValidatePackageFields(request.PackageName, request.WeightGram, request.LengthCm, request.WidthCm, request.HeightCm, request.PackageImageUrl);
        await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
        // The order is the serialization point shared with approval and assignment.
        var linkedOrder = _db.Database.IsNpgsql()
            ? (await _db.DeliveryOrders.FromSqlInterpolated($"SELECT * FROM delivery_orders WHERE \"PackageId\" = {packageId} FOR UPDATE").ToListAsync()).SingleOrDefault()
            : await _db.DeliveryOrders.SingleOrDefaultAsync(o => o.PackageId == packageId);
        if (linkedOrder is not null) await _db.Entry(linkedOrder).ReloadAsync();
        var package = _db.Database.IsNpgsql()
            ? (await _db.Packages.FromSqlInterpolated($"SELECT * FROM packages WHERE \"Id\" = {packageId} FOR UPDATE").ToListAsync()).SingleOrDefault()
            : await _db.Packages.SingleOrDefaultAsync(p => p.Id == packageId);
        if (package is null || (userRole == AppRoles.Customer && package.SenderId != userId))
            throw new ApiException(StatusCodes.Status404NotFound, "Package not found.");
        await _db.Entry(package).ReloadAsync();
        // A newly created link between the two locks must retry with order-first
        // locking; do not acquire an order lock while holding a package row lock.
        if (linkedOrder is null && await _db.DeliveryOrders.AnyAsync(o => o.PackageId == packageId))
            throw new ApiException(StatusCodes.Status409Conflict, "The package was linked to an order concurrently. Refresh and retry.");
        if (linkedOrder is not null && linkedOrder.Status != "PENDING")
            throw new ApiException(StatusCodes.Status409Conflict,
                $"Cannot modify package because associated delivery order is in status '{linkedOrder.Status}'.");

        var oldImageUrl = package.PackageImageUrl;
        var newImageUrl = string.IsNullOrWhiteSpace(request.PackageImageUrl) ? null : request.PackageImageUrl.Trim();

        package.PackageName = request.PackageName.Trim();
        package.WeightGram = request.WeightGram;
        package.LengthCm = request.LengthCm;
        package.WidthCm = request.WidthCm;
        package.HeightCm = request.HeightCm;
        package.IsFragile = request.IsFragile;
        package.PackageImageUrl = newImageUrl;

        await _db.SaveChangesAsync();
        if (tx is not null) await tx.CommitAsync();

        if (!string.IsNullOrEmpty(oldImageUrl) &&
            !string.Equals(oldImageUrl, newImageUrl, StringComparison.OrdinalIgnoreCase) &&
            IsOwnedStorageObject(package.Id, oldImageUrl))
        {
            await _storage.DeleteAsync(oldImageUrl);
        }

        return ToDto(package);
    }

    public async Task DeleteAsync(Guid packageId, Guid userId, string userRole)
    {
        await EnsureActiveUserAsync(userId);

        var package = await _db.Packages
            .Include(p => p.DeliveryOrder)
            .FirstOrDefaultAsync(p => p.Id == packageId);

        if (package == null || (userRole == AppRoles.Customer && package.SenderId != userId))
            throw new ApiException(StatusCodes.Status404NotFound, "Package not found.");

        if (package.DeliveryOrder != null)
            throw new ApiException(StatusCodes.Status409Conflict, "Cannot delete package because it is linked to a delivery order.");

        var imageUrl = package.PackageImageUrl;

        _db.Packages.Remove(package);
        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(imageUrl) && IsOwnedStorageObject(package.Id, imageUrl))
        {
            await _storage.DeleteAsync(imageUrl);
        }
    }

    public async Task<PackageImageResponse> UploadImageAsync(Guid packageId, Guid userId, string userRole, IFormFile? file)
    {
        await EnsureActiveUserAsync(userId);

        if (file == null || file.Length == 0)
            throw FileError("No file was uploaded.");

        if (file.Length > MaxImageBytes)
            throw FileError("Image file size exceeds the 5 MB limit.");

        var header = new byte[12];
        int read;
        await using (var probe = file.OpenReadStream())
            read = await probe.ReadAsync(header.AsMemory(0, header.Length));

        var detected = DetectImage(header.AsSpan(0, read));
        if (detected is null)
            throw FileError("Unsupported image type. Use JPEG, PNG, or WebP.");

        var package = await _db.Packages
            .Include(p => p.DeliveryOrder)
            .FirstOrDefaultAsync(p => p.Id == packageId);

        if (package == null || (userRole == AppRoles.Customer && package.SenderId != userId))
            throw new ApiException(StatusCodes.Status404NotFound, "Package not found.");

        if (package.DeliveryOrder != null && package.DeliveryOrder.Status != "PENDING")
        {
            throw new ApiException(StatusCodes.Status409Conflict,
                $"Cannot modify package because associated delivery order is in status '{package.DeliveryOrder.Status}'.");
        }

        await using var stream = file.OpenReadStream();
        var imageUrl = await _storage.UploadAsync(package.Id, stream, file.Length, detected.Value.ContentType, detected.Value.Extension);

        var previous = package.PackageImageUrl;
        package.PackageImageUrl = imageUrl;
        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(previous) &&
            !string.Equals(previous, imageUrl, StringComparison.OrdinalIgnoreCase) &&
            IsOwnedStorageObject(package.Id, previous))
        {
            await _storage.DeleteAsync(previous);
        }

        return new PackageImageResponse(imageUrl);
    }

    private async Task EnsureActiveUserAsync(Guid userId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");

        if (user.Status != "ACTIVE")
            throw new ApiException(StatusCodes.Status403Forbidden, "This account is not active.");
    }

    private static bool IsOwnedStorageObject(Guid packageId, string imageUrl)
    {
        var prefix = $"/{packageId:D}.";
        return imageUrl.Contains(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static (string ContentType, string Extension)? DetectImage(ReadOnlySpan<byte> h)
    {
        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
            && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A)
            return ("image/png", "png");

        // JPEG: FF D8 FF
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
            return ("image/jpeg", "jpg");

        // WebP: "RIFF" .... "WEBP"
        if (h.Length >= 12 && h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46
            && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50)
            return ("image/webp", "webp");

        return null;
    }

    private static ApiException FileError(string message) =>
        new(StatusCodes.Status400BadRequest, "One or more fields are invalid.",
            new Dictionary<string, string[]> { ["file"] = [message] });

    public static void ValidatePackageFields(
        string? packageName,
        decimal weightGram,
        decimal lengthCm,
        decimal widthCm,
        decimal heightCm,
        string? packageImageUrl = null)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(packageName))
            errors["packageName"] = ["Package name is required."];
        else if (packageName.Trim().Length > 200)
            errors["packageName"] = ["Package name must be at most 200 characters."];

        if (weightGram <= 0m || weightGram > 3000m)
            errors["weightGram"] = ["Weight must be greater than 0 and at most 3000 grams."];

        if (lengthCm <= 0m || lengthCm > 30m)
            errors["lengthCm"] = ["Length must be greater than 0 and at most 30 cm."];

        if (widthCm <= 0m || widthCm > 20m)
            errors["widthCm"] = ["Width must be greater than 0 and at most 20 cm."];

        if (heightCm <= 0m || heightCm > 15m)
            errors["heightCm"] = ["Height must be greater than 0 and at most 15 cm."];

        if (!string.IsNullOrWhiteSpace(packageImageUrl))
        {
            if (!Uri.TryCreate(packageImageUrl.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors["packageImageUrl"] = ["Package image URL must be a valid HTTP or HTTPS URL."];
            }
        }

        if (errors.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "One or more fields are invalid.", errors);
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
