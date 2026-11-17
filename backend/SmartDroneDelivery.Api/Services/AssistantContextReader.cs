using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;

namespace SmartDroneDelivery.Api.Services;

public record AssistantTimelineEvent(long Id, string? FromStatus, string ToStatus, DateTime CreatedAt);
public record AssistantStoredMessage(long Id, string SenderType, string MessageText, DateTime CreatedAt);
public record AssistantSnapshot(Guid OrderId, Guid OwnerId, string Status, DateTime OrderUpdatedAt, DateTime? EstimatedAt,
    DateTime? ActualAt, AssistantTimelineEvent[] Events, Guid? SessionId, DateTime? SessionCreatedAt,
    AssistantStoredMessage[] Messages, int MessageCount)
{
    public string Stamp => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { OrderId, OwnerId, Status, OrderUpdatedAt, EstimatedAt, ActualAt, Events, SessionId, SessionCreatedAt, Messages, MessageCount })));
    public AssistantContextMessage[] ContextHistory()
    {
        var safe = Messages.Where(m => m.SenderType is "USER" or "BOT"
                && m.MessageText.EnumerateRunes().Take(4001).Count() <= 4000 && !AssistantInput.IsSensitive(m.MessageText))
            .Select(m => new AssistantContextMessage(m.SenderType, m.MessageText)).ToList();
        var size = safe.Sum(m => m.MessageText.EnumerateRunes().Count());
        while (size > 12000 && safe.Count > 0)
        { size -= safe[0].MessageText.EnumerateRunes().Count(); safe.RemoveAt(0); }
        return safe.ToArray();
    }
}

public class AssistantContextReader(AppDbContext db, AssistantAccess access)
{
    public async Task<AssistantSnapshot> ReadAsync(Guid userId, Guid? sessionId, Guid orderId, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct) : null;
        await access.RequireCustomerAsync(userId, ct);
        DateTime? sessionCreated = null; var messageCount = 0; AssistantStoredMessage[] messages = [];
        if (sessionId is { } session)
        {
            await access.RequireSessionAsync(userId, session, ct);
            sessionCreated = await db.AiChatSessions.AsNoTracking().Where(s => s.Id == session && s.UserId == userId)
                .Select(s => s.CreatedAt).SingleAsync(ct);
            var history = db.AiChatMessages.AsNoTracking().Where(m => m.SessionId == session && m.Session.UserId == userId);
            messageCount = await history.CountAsync(ct);
            messages = await history.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(20)
                .Select(m => new AssistantStoredMessage(m.Id, m.SenderType, m.MessageText, m.CreatedAt)).ToArrayAsync(ct);
            Array.Reverse(messages);
        }
        var row = await db.DeliveryOrders.AsNoTracking().Where(o => o.Id == orderId && o.CustomerId == userId)
            .Select(o => new { o.CustomerId, o.Status, o.UpdatedAt, o.EstimatedDeliveryTime, o.ActualDeliveryTime }).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(404, "Delivery order not found.");
        var events = await db.DeliveryStatusLogs.AsNoTracking().Where(l => l.OrderId == orderId)
            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id).Take(20)
            .Select(l => new AssistantTimelineEvent(l.Id, l.FromStatus, l.ToStatus, l.CreatedAt)).ToArrayAsync(ct);
        Array.Reverse(events);
        if (tx is not null) await tx.CommitAsync(ct);
        return new(orderId, row.CustomerId, row.Status, row.UpdatedAt, row.EstimatedDeliveryTime, row.ActualDeliveryTime,
            events, sessionId, sessionCreated, messages, messageCount);
    }
}
