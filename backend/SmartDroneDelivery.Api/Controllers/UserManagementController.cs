using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Models;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = AppRoles.Admin)]
public class UserManagementController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public UserManagementController(AppDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllUsers(CancellationToken cancellationToken)
    {
        await RequireActiveUserAsync(cancellationToken);
        var users = await _context.Users.AsNoTracking().Include(u => u.Role)
            .OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id)
            .ToListAsync(cancellationToken);
        return Ok(users.Select(UserResponse.From));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken cancellationToken)
    {
        await RequireActiveUserAsync(cancellationToken);
        var user = await _context.Users.AsNoTracking().Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "User not found.");
        return Ok(UserResponse.From(user));
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var actorId = await RequireActiveUserAsync(cancellationToken);
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = request.PhoneNumber.Trim();
        await EnsureUniqueDetailsAsync(email, phone, null, cancellationToken);

        var targetRole = await FindRoleAsync(request.Role, cancellationToken);
        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            PhoneNumber = phone,
            RoleId = targetRole.Id,
            Role = targetRole,
            Status = "ACTIVE"
        };
        _context.Users.Add(user);
        AddAudit(actorId, "CREATE_USER", user.Id);
        await SaveUserChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, UserResponse.From(user));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = await RequireActiveUserAsync(cancellationToken);
        var user = await _context.Users.Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "User not found.");
        var email = request.Email.Trim().ToLowerInvariant();
        var fullName = request.FullName.Trim();
        var phone = request.PhoneNumber.Trim();
        await EnsureUniqueDetailsAsync(email, phone, id, cancellationToken);

        if (user.Email == email && user.FullName == fullName && user.PhoneNumber == phone)
            return Ok(UserResponse.From(user));

        // Role, account status and password have their own dedicated workflows.
        user.Email = email;
        user.FullName = fullName;
        user.PhoneNumber = phone;
        user.UpdatedAt = DateTime.UtcNow;
        AddAudit(actorId, "USER_UPDATE", user.Id);
        await SaveUserChangesAsync(cancellationToken);
        return Ok(UserResponse.From(user));
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateUser(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await LockAdminChangesAsync(cancellationToken);
        var actorId = await RequireActiveUserAsync(cancellationToken);
        if (actorId == id)
            throw new ApiException(StatusCodes.Status409Conflict, "You cannot deactivate your own account.");
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "User not found.");
        await EnsureAnotherAdminAsync(user, cancellationToken);
        var tokens = await _context.RefreshTokens.Where(t => t.UserId == id && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        // Retain the account, related business history and audit records; do not delete the user.
        if (user.Status != "INACTIVE")
        {
            user.Status = "INACTIVE";
            user.UpdatedAt = DateTime.UtcNow;
            AddAudit(actorId, "USER_DEACTIVATE", user.Id);
        }
        foreach (var token in tokens)
            token.IsRevoked = true;

        // Retrying is safe: a previously deactivated account does not produce a duplicate audit.
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    private async Task SaveUserChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // The account and its audit entry are saved in the same database transaction.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_Email" })
        {
            throw new ApiException(StatusCodes.Status409Conflict, "Email is already registered.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_PhoneNumber" })
        {
            throw new ApiException(StatusCodes.Status409Conflict, "Phone number is already registered.");
        }
    }

    private async Task EnsureUniqueDetailsAsync(string email, string phone, Guid? excludingUserId,
        CancellationToken cancellationToken)
    {
        var otherUsers = _context.Users.AsQueryable();
        if (excludingUserId is Guid id)
            otherUsers = otherUsers.Where(u => u.Id != id);
        if (await otherUsers.AnyAsync(u => u.Email.ToLower() == email, cancellationToken))
            throw new ApiException(StatusCodes.Status409Conflict, "Email is already registered.");
        if (await otherUsers.AnyAsync(u => u.PhoneNumber == phone, cancellationToken))
            throw new ApiException(StatusCodes.Status409Conflict, "Phone number is already registered.");
    }

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> UpdateUserRole(Guid id, [FromBody] UpdateUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await LockAdminChangesAsync(cancellationToken);
        var actorId = await RequireActiveUserAsync(cancellationToken);
        var user = await _context.Users.Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "User not found.");
        var targetRole = await FindRoleAsync(request.Role, cancellationToken);
        if (user.RoleId == targetRole.Id)
            return NoContent();
        if (actorId == id)
            throw new ApiException(StatusCodes.Status409Conflict, "You cannot change your own administrator role.");
        if (targetRole.Code != AppRoles.Admin)
            await EnsureAnotherAdminAsync(user, cancellationToken);

        user.RoleId = targetRole.Id;
        user.Role = targetRole;
        user.UpdatedAt = DateTime.UtcNow;
        AddAudit(actorId, "ROLE_CHANGE", user.Id);
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    private async Task<IDbContextTransaction?> LockAdminChangesAsync(CancellationToken cancellationToken)
    {
        // Serialize role/deactivation changes before checking the actor and remaining admins.
        // Otherwise two admins could deactivate/demote each other concurrently.
        // The HTTP test factory uses EF InMemory, which has no relational transactions.
        if (!_context.Database.IsNpgsql())
            return null;
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.Database.ExecuteSqlRawAsync(
                """SELECT "Id" FROM users WHERE "RoleId" = (SELECT "Id" FROM roles WHERE "Code" = 'ADMIN') ORDER BY "Id" FOR UPDATE""",
                cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private async Task EnsureAnotherAdminAsync(User user, CancellationToken cancellationToken)
    {
        var adminRoleId = await _context.Roles.Where(r => r.Code == AppRoles.Admin)
            .Select(r => r.Id).SingleAsync(cancellationToken);
        if (user.Status == "ACTIVE" && user.RoleId == adminRoleId &&
            !await _context.Users.AnyAsync(u => u.Id != user.Id && u.RoleId == adminRoleId && u.Status == "ACTIVE",
                cancellationToken))
            throw new ApiException(StatusCodes.Status409Conflict, "The last active administrator cannot be removed.");
    }

    private async Task<Role> FindRoleAsync(string value, CancellationToken cancellationToken)
    {
        var role = value.Trim();
        var code = role.ToUpperInvariant();
        // Accept role codes; retain support for the display names accepted by the original endpoint.
        var target = await _context.Roles.SingleOrDefaultAsync(r => r.Code == code, cancellationToken)
            ?? await _context.Roles.FirstOrDefaultAsync(r => r.Name == role, cancellationToken);
        if (target is null || !AppRoles.All.Contains(target.Code))
            throw new ApiException(StatusCodes.Status400BadRequest, "Invalid role.",
                new Dictionary<string, string[]> { ["role"] = new[] { "Select a supported role code." } });
        return target;
    }

    private async Task<Guid> RequireActiveUserAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id))
            throw new ApiException(StatusCodes.Status401Unauthorized, "Invalid user identity.");
        var account = await _context.Users.Where(u => u.Id == id)
            .Select(u => new { u.Status, RoleCode = u.Role.Code })
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null)
            throw new ApiException(StatusCodes.Status401Unauthorized, "User no longer exists.");
        if (account.Status != "ACTIVE")
            throw new ApiException(StatusCodes.Status403Forbidden, "User account is not active.");
        if (account.RoleCode != AppRoles.Admin)
            throw new ApiException(StatusCodes.Status403Forbidden, "You do not have permission to perform this action.");
        return id;
    }

    private void AddAudit(Guid actorId, string action, Guid targetId) => _context.AuditLogs.Add(new AuditLog
    {
        UserId = actorId,
        Action = action,
        EntityName = "User",
        EntityId = targetId.ToString(),
        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        UserAgent = Request.Headers.UserAgent.ToString()
    });
}
