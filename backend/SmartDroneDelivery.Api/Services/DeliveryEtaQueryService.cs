using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public class DeliveryEtaQueryService(AppDbContext db)
{
    public async Task<DeliveryEtaDto> GetAsync(Guid userId, Guid orderId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.Status, Role = u.Role.Code }).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(401, "Missing, invalid or expired token.");
        if (user.Status != "ACTIVE" || user.Role is not
            (AppRoles.Customer or AppRoles.Dispatcher or AppRoles.Manager))
            throw new ApiException(403, "You do not have permission to read delivery estimates.");

        // Staff get only this redacted view, never the customer detail DTO.
        var row = await db.DeliveryOrders.AsNoTracking()
            .Where(o => o.Id == orderId && (user.Role != AppRoles.Customer || o.CustomerId == userId))
            .Select(o => new { o.Id, o.Status, o.EstimatedDeliveryTime, o.ActualDeliveryTime, o.AiEtaPrediction })
            .SingleOrDefaultAsync(ct)
            ?? throw new ApiException(404, "Delivery order not found.");
        return new(row.Id, row.Status, Utc(row.EstimatedDeliveryTime), Utc(row.ActualDeliveryTime),
            AiEtaPredictionDto.From(row.AiEtaPrediction));
    }

    private static DateTime? Utc(DateTime? value) => value.HasValue
        ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
