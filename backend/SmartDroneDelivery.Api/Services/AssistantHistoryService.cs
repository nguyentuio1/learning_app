using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Api.Services;

public class AssistantHistoryService(AppDbContext db, AssistantAccess access, IOptions<CustomerAssistantOptions> options, TimeProvider clock)
{
    public async Task<AssistantSessionsDto> SessionsAsync(Guid userId, AssistantPageQuery page, CancellationToken ct)
    {
        await access.RequireCustomerAsync(userId, ct);
        var cutoff = options.Value.HasApprovedRetention ? clock.GetUtcNow().UtcDateTime.AddDays(-options.Value.RetentionDays) : DateTime.MinValue;
        var query = db.AiChatSessions.AsNoTracking().Where(s => s.UserId == userId && s.CreatedAt >= cutoff);
        var count = await query.CountAsync(ct);
        var offset = (long)(page.Page - 1) * page.PageSize;
        var rows = offset >= count ? [] : await query.OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            .Skip((int)offset).Take(page.PageSize).Select(s => new { s.Id, s.CreatedAt }).ToArrayAsync(ct);
        return new(rows.Select(s => new AssistantSessionDto(s.Id, null, Utc(s.CreatedAt))).ToArray(), page.Page, page.PageSize, count);
    }

    public async Task<AssistantMessagesDto> MessagesAsync(Guid userId, Guid sessionId, AssistantPageQuery page, CancellationToken ct)
    {
        await access.RequireCustomerAsync(userId, ct); await access.RequireSessionAsync(userId, sessionId, ct);
        var query = db.AiChatMessages.AsNoTracking().Where(m => m.SessionId == sessionId && m.Session.UserId == userId);
        var count = await query.CountAsync(ct);
        var offset = (long)(page.Page - 1) * page.PageSize;
        var rows = offset >= count ? [] : await query.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Skip((int)offset).Take(page.PageSize).Select(m => new { m.Id, m.SenderType, m.MessageText, m.CreatedAt }).ToArrayAsync(ct);
        if (rows.Any(m => m.SenderType is not ("USER" or "BOT"))) throw new ApiException(503, "Conversation history is unavailable.");
        return new(sessionId, rows.Select(m => new AssistantReplyDto(m.Id.ToString(CultureInfo.InvariantCulture),
            m.SenderType, AssistantInput.SafeHistory(m.MessageText), Utc(m.CreatedAt))).ToArray(), page.Page, page.PageSize, count);
    }
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
