using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public class IntegrationKeyService(AppDbContext db, TimeProvider clock)
{
    public const string CreateScope = "orders:create";
    public const string ReadScope = "orders:read";
    public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public async Task<CreatedIntegrationKey> CreateAsync(Guid admin, CreateIntegrationKeyRequest request, CancellationToken ct = default)
    {
        await RequireAdminAsync(admin,ct);
        var errors = new Dictionary<string,string[]>();
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100) errors["name"] = ["Name is required and must be at most 100 characters."];
        if (request.CustomerId == Guid.Empty) errors["customerId"] = ["A customer UUID is required."];
        if (request.Scopes is not {Length:>0} || request.Scopes.Any(s => s is not (CreateScope or ReadScope)) || request.Scopes.Distinct().Count() != request.Scopes.Length)
            errors["scopes"] = ["Provide a nonempty unique subset of orders:create and orders:read."];
        var now = clock.GetUtcNow().UtcDateTime;
        if (request.ExpiresAt is not {} expires || expires.Kind != DateTimeKind.Utc || expires <= now)
            errors["expiresAt"] = ["A future UTC timestamp ending in Z is required."];
        if (errors.Count > 0) throw new ApiException(400,"One or more fields are invalid.",errors);
        if (!await EligibleAsync(request.CustomerId,ct)) throw new ApiException(409,"Target must be an active CUSTOMER account.");
        var secret = "sdd_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var key = new IntegrationApiKey {CustomerId=request.CustomerId,Name=name!,KeyHash=Hash(secret),Scopes=request.Scopes!.ToArray(),CreatedAt=now,ExpiresAt=request.ExpiresAt!.Value};
        db.IntegrationApiKeys.Add(key);
        db.AuditLogs.Add(Audit(admin,"INTEGRATION_KEY_CREATE",key.Id,now));
        // One SaveChanges is atomic: key metadata/hash and its audit, or neither.
        await db.SaveChangesAsync(ct);
        return new(key.Id,key.Name,key.CustomerId,key.Scopes,key.CreatedAt,key.ExpiresAt,null,secret);
    }

    public async Task<IReadOnlyList<IntegrationKeyMetadata>> ListAsync(Guid admin, Guid? customer, CancellationToken ct = default)
    {
        await RequireAdminAsync(admin,ct);
        return await db.IntegrationApiKeys.AsNoTracking().Where(k=>customer==null || k.CustomerId==customer)
            .OrderByDescending(k=>k.CreatedAt).ThenByDescending(k=>k.Id)
            .Select(k=>new IntegrationKeyMetadata(k.Id,k.Name,k.CustomerId,k.Scopes,k.CreatedAt,k.ExpiresAt,k.RevokedAt)).ToListAsync(ct);
    }

    public async Task RevokeAsync(Guid admin, Guid id, CancellationToken ct = default)
    {
        await RequireAdminAsync(admin,ct);
        var key = await db.IntegrationApiKeys.SingleOrDefaultAsync(k=>k.Id==id,ct) ?? throw new ApiException(404,"Integration key not found.");
        if (key.RevokedAt != null) return;
        key.RevokedAt = clock.GetUtcNow().UtcDateTime;
        db.AuditLogs.Add(Audit(admin,"INTEGRATION_KEY_REVOKE",key.Id,key.RevokedAt.Value));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) {
            // A concurrent revocation won. The failed SaveChanges rolled back its audit.
            db.ChangeTracker.Clear();
            if (!await db.IntegrationApiKeys.AsNoTracking().AnyAsync(k=>k.Id==id && k.RevokedAt!=null,ct)) throw;
        }
    }

    public async Task<Guid> RequireAccessAsync(Guid id, string scope, CancellationToken ct = default)
    {
        var key = await db.IntegrationApiKeys.AsNoTracking().SingleOrDefaultAsync(k=>k.Id==id,ct);
        if (key == null || key.RevokedAt != null || key.ExpiresAt <= clock.GetUtcNow().UtcDateTime)
            throw new ApiException(401,"Missing, invalid or expired API key.");
        if (!key.Scopes.Contains(scope) || !await EligibleAsync(key.CustomerId,ct))
            throw new ApiException(403,"API key scope or bound customer access is denied.");
        return key.CustomerId;
    }

    private Task<bool> EligibleAsync(Guid id,CancellationToken ct) => db.Users.AsNoTracking().AnyAsync(u=>u.Id==id && u.Status=="ACTIVE" && u.Role.Code==AppRoles.Customer,ct);
    private async Task RequireAdminAsync(Guid id,CancellationToken ct) {
        if (!await db.Users.AsNoTracking().AnyAsync(u=>u.Id==id && u.Status=="ACTIVE" && u.Role.Code==AppRoles.Admin,ct))
            throw new ApiException(403,"An active ADMIN account is required.");
    }
    private static AuditLog Audit(Guid actor,string action,Guid key,DateTime now)=>new(){UserId=actor,Action=action,EntityName="IntegrationApiKey",EntityId=key.ToString(),Timestamp=now};
}
