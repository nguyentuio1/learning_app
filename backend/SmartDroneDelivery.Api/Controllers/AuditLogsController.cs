using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

public record AuditLogItem(long Id, DateTime Timestamp, Guid? UserId, string? UserEmail, string? UserFullName,
    string Action, string EntityName, string EntityId, string? IpAddress, string? UserAgent, string? Reason);

public record AuditLogPage(IReadOnlyList<AuditLogItem> Items, int Page, int PageSize, int TotalCount, int TotalPages);

/// <summary>
/// LA-177 / FR-33 (F-19): read-only audit log search for administrators, newest first, by user, action,
/// entity and time range. The audit rows never hold passwords, tokens or pickup codes (BR-UA-19).
/// </summary>
[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = AppRoles.Admin)]
public class AuditLogsController(AppDbContext db) : ControllerBase
{
    public const int MaxPageSize = 100;
    private static readonly string[] Allowed = ["userId", "action", "entityName", "entityId", "from", "to", "page", "pageSize"];

    [HttpGet]
    public async Task<ActionResult<AuditLogPage>> Search(CancellationToken ct)
    {
        await RequireActiveAdminAsync(ct);
        var q = Request.Query;
        var errors = new Dictionary<string, string[]>();
        foreach (var (key, values) in q)
            if (!Allowed.Contains(key, StringComparer.Ordinal) || values.Count != 1)
                errors[key] = ["Unknown or repeated query parameter."];

        Guid? userId = null;
        if (q.ContainsKey("userId"))
        {
            if (Guid.TryParse(q["userId"], out var parsed)) userId = parsed;
            else errors["userId"] = ["Must be a user id (GUID)."];
        }
        DateTime? from = ReadTime("from"), to = ReadTime("to");
        if (from is not null && to is not null && to < from) errors["to"] = ["Must not be earlier than from."];
        var page = ReadInt("page", 1, int.MaxValue);
        var pageSize = ReadInt("pageSize", 20, MaxPageSize);
        var action = ReadText("action", 100)?.ToUpperInvariant();
        var entityName = ReadText("entityName", 50);
        var entityId = ReadText("entityId", 100);
        if (errors.Count > 0) throw new ApiException(StatusCodes.Status400BadRequest, "One or more query parameters are invalid.", errors);

        var logs = db.AuditLogs.AsNoTracking();
        if (userId is not null) logs = logs.Where(a => a.UserId == userId);
        if (action is not null) logs = logs.Where(a => a.Action == action);
        if (entityName is not null) logs = logs.Where(a => a.EntityName == entityName);
        if (entityId is not null) logs = logs.Where(a => a.EntityId == entityId);
        if (from is not null) logs = logs.Where(a => a.Timestamp >= from);
        if (to is not null) logs = logs.Where(a => a.Timestamp <= to);

        var total = await logs.CountAsync(ct);
        var offset = (long)(page - 1) * pageSize;
        var items = offset >= total
            ? []
            : await logs.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
                .Skip((int)offset).Take(pageSize)
                .Select(a => new AuditLogItem(a.Id, DateTime.SpecifyKind(a.Timestamp, DateTimeKind.Utc), a.UserId,
                    a.User == null ? null : a.User.Email, a.User == null ? null : a.User.FullName,
                    a.Action, a.EntityName, a.EntityId, a.IpAddress, a.UserAgent, a.Reason))
                .ToListAsync(ct);
        return Ok(new AuditLogPage(items, page, pageSize, total, (int)((total + (long)pageSize - 1) / pageSize)));

        DateTime? ReadTime(string name)
        {
            if (!q.ContainsKey(name)) return null;
            if (DateTimeOffset.TryParse(q[name], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value))
                return value.UtcDateTime;
            errors[name] = ["Must be an ISO 8601 date or date-time (UTC unless an offset is given)."];
            return null;
        }
        int ReadInt(string name, int fallback, int max)
        {
            if (!q.ContainsKey(name)) return fallback;
            if (int.TryParse(q[name], NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1 && value <= max) return value;
            errors[name] = [$"Must be an integer between 1 and {max}."];
            return fallback;
        }
        string? ReadText(string name, int maxLength)
        {
            if (!q.ContainsKey(name)) return null;
            var value = q[name].ToString().Trim();
            if (value.Length is > 0 && value.Length <= maxLength) return value;
            errors[name] = [$"Must be 1 to {maxLength} characters."];
            return null;
        }
    }

    /// <summary>Distinct action codes present in the log, for the filter list.</summary>
    [HttpGet("actions")]
    public async Task<ActionResult<IReadOnlyList<string>>> Actions(CancellationToken ct)
    {
        await RequireActiveAdminAsync(ct);
        return Ok(await db.AuditLogs.AsNoTracking().Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(ct));
    }

    private async Task RequireActiveAdminAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id))
            throw new ApiException(StatusCodes.Status401Unauthorized, "Invalid user identity.");
        var account = await db.Users.Where(u => u.Id == id).Select(u => new { u.Status, RoleCode = u.Role.Code }).SingleOrDefaultAsync(ct);
        if (account is null) throw new ApiException(StatusCodes.Status401Unauthorized, "User no longer exists.");
        if (account.Status != "ACTIVE") throw new ApiException(StatusCodes.Status403Forbidden, "User account is not active.");
        if (account.RoleCode != AppRoles.Admin) throw new ApiException(StatusCodes.Status403Forbidden, "You do not have permission to perform this action.");
    }
}
