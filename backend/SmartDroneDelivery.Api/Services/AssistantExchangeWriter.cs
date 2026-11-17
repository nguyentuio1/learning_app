using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

public class AssistantExchangeWriter(AppDbContext db, AssistantContextReader reader, TimeProvider clock)
{
    public async Task<AssistantResponseDto> CommitAsync(Guid userId, AssistantSnapshot original, string message, AssistantIntent intent, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (db.Database.IsNpgsql())
        {
            // All assistant mutations lock user then session; inference holds neither locks nor transaction.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM users WHERE \"Id\"={userId} FOR UPDATE", ct);
            if (original.SessionId is { } existing)
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM ai_chat_sessions WHERE \"Id\"={existing} FOR UPDATE", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM delivery_orders WHERE \"Id\"={original.OrderId} FOR UPDATE", ct);
            var logIds = original.Events.Select(e => e.Id).ToArray();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM delivery_status_logs WHERE \"Id\"=ANY({logIds}) ORDER BY \"Id\" FOR SHARE", ct);
        }
        var current = await reader.ReadAsync(userId, original.SessionId, original.OrderId, ct);
        if (current.Stamp != original.Stamp) throw new ApiException(409, "The conversation or delivery changed. Refresh before trying again.");
        var now = clock.GetUtcNow().UtcDateTime;
        var text = AssistantReplyRenderer.Render(intent, current, now);
        var sessionId = original.SessionId ?? Guid.NewGuid();
        if (original.SessionId is null) db.AiChatSessions.Add(new AiChatSession { Id = sessionId, UserId = userId, SessionTitle = null, CreatedAt = now });
        var userMessage = new AiChatMessage { SessionId = sessionId, SenderType = "USER", MessageText = message, CreatedAt = now };
        db.AiChatMessages.Add(userMessage);
        // Separate inserts make USER precede BOT by numeric identity; one outer transaction keeps the pair atomic.
        await db.SaveChangesAsync(ct);
        var reply = new AiChatMessage { SessionId = sessionId, SenderType = "BOT", MessageText = text, CreatedAt = now };
        db.AiChatMessages.Add(reply); await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return new("ANSWERED", sessionId, userMessage.Id.ToString(CultureInfo.InvariantCulture),
            new(reply.Id.ToString(CultureInfo.InvariantCulture), "BOT", text, now));
    }
}
