using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

public interface IUserService
{
    Task<UserDto> GetAsync(Guid userId);
    Task<UserDto> UpdateAsync(Guid userId, UpdateProfileRequest request);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, ClientInfo client);
    Task<AvatarResponse> UploadAvatarAsync(Guid userId, IFormFile? file);
}

public class UserService : IUserService
{
    public const long MaxAvatarBytes = 5 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IAvatarStorage _avatars;

    public UserService(AppDbContext db, IPasswordHasher hasher, IAvatarStorage avatars)
    {
        _db = db;
        _hasher = hasher;
        _avatars = avatars;
    }

    public async Task<UserDto> GetAsync(Guid userId) => UserDto.From(await LoadActiveUserAsync(userId));

    public async Task<UserDto> UpdateAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await LoadActiveUserAsync(userId);
        var phone = request.PhoneNumber.Trim();

        if (await _db.Users.AnyAsync(u => u.PhoneNumber == phone && u.Id != userId))
            throw new ApiException(StatusCodes.Status409Conflict, "Phone number is already registered.");

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = phone;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return UserDto.From(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, ClientInfo client)
    {
        var user = await LoadActiveUserAsync(userId);

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ApiException(StatusCodes.Status401Unauthorized, "Current password is incorrect.");

        if (request.NewPassword == request.CurrentPassword)
            throw new ApiException(StatusCodes.Status400BadRequest, "One or more validation errors occurred.",
                new Dictionary<string, string[]> { ["newPassword"] = new[] { "New password must be different from the current password." } });

        var now = DateTime.UtcNow;
        user.PasswordHash = _hasher.Hash(request.NewPassword);
        user.UpdatedAt = now;

        // Every device has to sign in again with the new password.
        var active = await _db.RefreshTokens.Where(t => t.UserId == userId && !t.IsRevoked).ToListAsync();
        foreach (var token in active)
            token.IsRevoked = true;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "PASSWORD_CHANGE",
            EntityName = "User",
            EntityId = userId.ToString(),
            IpAddress = client.IpAddress,
            UserAgent = client.UserAgent
        });
        await _db.SaveChangesAsync();
    }

    public async Task<AvatarResponse> UploadAvatarAsync(Guid userId, IFormFile? file)
    {
        var user = await LoadActiveUserAsync(userId);

        if (file is null || file.Length == 0)
            throw FileError("A file is required.");
        if (file.Length > MaxAvatarBytes)
            throw FileError("The file must be at most 5 MB.");

        // Decide the type from the first bytes, not from the name or content type the client sent.
        var header = new byte[8];
        int read;
        await using (var probe = file.OpenReadStream())
            read = await probe.ReadAsync(header.AsMemory(0, header.Length));
        var type = DetectImage(header.AsSpan(0, read));
        if (type is null)
            throw FileError("Only JPEG and PNG images are allowed.");

        await using var content = file.OpenReadStream();
        var url = await _avatars.UploadAsync(userId, content, file.Length, type.Value.ContentType, type.Value.Extension);

        var previous = user.AvatarUrl;
        user.AvatarUrl = url;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // A PNG replaced by a JPEG leaves a different file name behind.
        if (!string.IsNullOrEmpty(previous) && !string.Equals(previous, url, StringComparison.OrdinalIgnoreCase))
            await _avatars.DeleteAsync(previous);

        return new AvatarResponse(url);
    }

    private async Task<User> LoadActiveUserAsync(Guid userId)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");

        if (user.Status != "ACTIVE")
            throw new ApiException(StatusCodes.Status403Forbidden, "This account is not active.");
        return user;
    }

    private static ApiException FileError(string message) =>
        new(StatusCodes.Status400BadRequest, "One or more validation errors occurred.",
            new Dictionary<string, string[]> { ["file"] = new[] { message } });

    private static (string ContentType, string Extension)? DetectImage(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
            && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A)
            return ("image/png", "png");
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
            return ("image/jpeg", "jpg");
        return null;
    }
}
