using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Api.Services;

public class AssistantRetentionService(AppDbContext db, AssistantAccess access, IOptions<CustomerAssistantOptions> options, TimeProvider clock)
{
    public async Task<int> CleanupBatchAsync(CancellationToken ct)
    {
        if (!options.Value.HasApprovedRetention) return 0;
        var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-options.Value.RetentionDays);
        var ids = await db.AiChatSessions.AsNoTracking().Where(s => s.CreatedAt < cutoff)
            .OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).Take(100).Select(s => s.Id).ToArrayAsync(ct);
        if (ids.Length == 0) return 0;
        return await RemoveAsync(db.AiChatSessions.Where(s => ids.Contains(s.Id) && s.CreatedAt < cutoff), ct);
    }
    public async Task DeleteAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        await access.RequireCustomerAsync(userId, ct);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM users WHERE \"Id\"={userId} FOR UPDATE", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM ai_chat_sessions WHERE \"Id\"={sessionId} FOR UPDATE", ct);
        }
        await access.RequireCustomerAsync(userId, ct);
        var removed = await RemoveAsync(db.AiChatSessions.Where(s => s.Id == sessionId && s.UserId == userId), ct);
        if (removed == 0) throw new ApiException(404, "Conversation not found.");
        if (tx is not null) await tx.CommitAsync(ct);
    }
    private async Task<int> RemoveAsync(IQueryable<SmartDroneDelivery.Api.Entities.AiChatSession> query, CancellationToken ct)
    {
        if (db.Database.IsRelational()) return await query.ExecuteDeleteAsync(ct); // DB cascade is atomic, including messages.
        var rows = await query.ToArrayAsync(ct); var ids = rows.Select(s => s.Id).ToArray();
        db.AiChatMessages.RemoveRange(await db.AiChatMessages.Where(m => ids.Contains(m.SessionId)).ToArrayAsync(ct));
        db.AiChatSessions.RemoveRange(rows); await db.SaveChangesAsync(ct); return rows.Length;
    }
}
