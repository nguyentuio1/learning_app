using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public partial class StationOperationService
{
    // Existing operator entry point keeps its station-scoped authorization and wire contract.
    public Task<PickupVerificationResult> PickupAsync(Guid userId, Guid stationId, ConfirmPickupRequest request) =>
        PickupCoreAsync(userId, stationId, request, null);

    public async Task<PickupVerificationResult> ConfirmDeliveryAsync(Guid userId, Guid orderId, ConfirmDeliveryRequest request)
    {
        await AuthorizeConfirmationCustomerAsync(userId);
        var order = await _db.DeliveryOrders.AsNoTracking()
            .Where(o => o.Id == orderId && o.CustomerId == userId)
            .Select(o => new { o.TrackingNumber, o.DestinationStationId }).SingleOrDefaultAsync()
            ?? throw ConfirmationNotFound();
        return await PickupCoreAsync(userId, order.DestinationStationId, new ConfirmPickupRequest {
            TrackingNumber = order.TrackingNumber, PickupSecureCode = request.PickupSecureCode
        }, orderId);
    }

    private async Task AuthorizePickupCallerAsync(Guid userId, Guid stationId, Guid? customerOrderId, DeliveryOrder? lockedOrder = null)
    {
        if (customerOrderId is not { } orderId) { await AuthorizeStationOperatorAsync(userId, stationId); return; }
        await AuthorizeConfirmationCustomerAsync(userId);
        if (lockedOrder is not null)
        {
            if (lockedOrder.Id != orderId || lockedOrder.CustomerId != userId || lockedOrder.DestinationStationId != stationId)
                throw ConfirmationNotFound();
        }
        else if (!await _db.DeliveryOrders.AsNoTracking().AnyAsync(o => o.Id == orderId && o.CustomerId == userId))
            throw ConfirmationNotFound();
    }

    private async Task AuthorizeConfirmationCustomerAsync(Guid userId)
    {
        var caller = await _db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.Status, Role = u.Role.Code }).SingleOrDefaultAsync()
            ?? throw new ApiException(401, "Missing, invalid or expired token.");
        if (caller.Status != "ACTIVE" || caller.Role != AppRoles.Customer)
            throw new ApiException(403, "An active customer account is required.");
    }

    private static ApiException ConfirmationNotFound() => new(404, "Delivery order not found.");
}
