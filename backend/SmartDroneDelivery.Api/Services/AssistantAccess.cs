using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Security;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Api.Services;

public class AssistantAccess(AppDbContext db, IOptions<CustomerAssistantOptions> options, TimeProvider clock)
{
    public async Task RequireCustomerAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.Status, Role = u.Role.Code }).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(401, "Missing, invalid or expired token.");
        if (user.Status != "ACTIVE" || user.Role != AppRoles.Customer)
            throw new ApiException(403, "You do not have permission to use this assistant.");
    }
    public async Task RequireSessionAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var cutoff = options.Value.HasApprovedRetention ? clock.GetUtcNow().UtcDateTime.AddDays(-options.Value.RetentionDays) : DateTime.MinValue;
        if (!await db.AiChatSessions.AsNoTracking().AnyAsync(s => s.Id == sessionId && s.UserId == userId && s.CreatedAt >= cutoff, ct))
            throw new ApiException(404, "Conversation not found.");
    }
    public async Task RequireOrderAsync(Guid userId, Guid orderId, CancellationToken ct)
    {
        if (!await db.DeliveryOrders.AsNoTracking().AnyAsync(o => o.Id == orderId && o.CustomerId == userId, ct))
            throw new ApiException(404, "Delivery order not found.");
    }
}
