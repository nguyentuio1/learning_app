using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, ClientInfo client);
    Task<AuthResponse> LoginAsync(LoginRequest request, ClientInfo client);
    Task<AuthResponse> RefreshAsync(string refreshToken);
    Task LogoutAsync(Guid userId, string refreshToken, ClientInfo client);
}

public record ClientInfo(string? IpAddress, string? UserAgent);

public class AuthService : IAuthService
{
    private const string CustomerRoleCode = "CUSTOMER";

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;

    public AuthService(AppDbContext db, IPasswordHasher hasher, ITokenService tokens)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, ClientInfo client)
    {
        var email = NormalizeEmail(request.Email);
        var phone = request.PhoneNumber.Trim();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            throw new ApiException(StatusCodes.Status409Conflict, "Email is already registered.");
        if (await _db.Users.AnyAsync(u => u.PhoneNumber == phone))
            throw new ApiException(StatusCodes.Status409Conflict, "Phone number is already registered.");

        var role = await _db.Roles.FirstAsync(r => r.Code == CustomerRoleCode);
        var now = DateTime.UtcNow;

        var user = new User
        {
            RoleId = role.Id,
            Role = role,
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            PhoneNumber = phone,
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Users.Add(user);

        var response = IssueTokens(user, now);
        AddAudit(user.Id, "REGISTER", client);
        await _db.SaveChangesAsync();
        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, ClientInfo client)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);

        // Same message for an unknown email and a wrong password.
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new ApiException(StatusCodes.Status401Unauthorized, "Invalid email or password.");

        if (user.Status != "ACTIVE")
            throw new ApiException(StatusCodes.Status403Forbidden, "This account is not active.");

        var response = IssueTokens(user, DateTime.UtcNow);
        AddAudit(user.Id, "LOGIN", client);
        await _db.SaveChangesAsync();
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken)
    {
        var now = DateTime.UtcNow;
        var stored = await _db.RefreshTokens
            .Include(t => t.User).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(t => t.Token == refreshToken);

        if (stored is null || stored.IsRevoked || stored.ExpiresAt <= now || stored.User.Status != "ACTIVE")
            throw new ApiException(StatusCodes.Status401Unauthorized, "Invalid refresh token.");

        // Rotation: the old token stops working as soon as the new pair is saved.
        stored.IsRevoked = true;
        var response = IssueTokens(stored.User, now);
        await _db.SaveChangesAsync();
        return response;
    }

    public async Task LogoutAsync(Guid userId, string refreshToken, ClientInfo client)
    {
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);

        // Only the owner can revoke a token; an unknown or foreign token is ignored so retrying is safe.
        if (stored is not null && stored.UserId == userId && !stored.IsRevoked)
            stored.IsRevoked = true;

        AddAudit(userId, "LOGOUT", client);
        await _db.SaveChangesAsync();
    }

    private AuthResponse IssueTokens(User user, DateTime now)
    {
        var refresh = new RefreshToken
        {
            User = user,
            Token = _tokens.CreateRefreshToken(),
            ExpiresAt = _tokens.RefreshTokenExpiry(now),
            CreatedAt = now
        };
        _db.RefreshTokens.Add(refresh);

        return new AuthResponse(
            _tokens.CreateAccessToken(user, now),
            refresh.Token,
            _tokens.AccessTokenLifetimeSeconds,
            UserDto.From(user));
    }

    private void AddAudit(Guid userId, string action, ClientInfo client)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = "User",
            EntityId = userId.ToString(),
            IpAddress = client.IpAddress,
            UserAgent = client.UserAgent
        });
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
