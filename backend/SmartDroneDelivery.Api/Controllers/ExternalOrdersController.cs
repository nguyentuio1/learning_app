using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Route("api/v1/delivery-orders")]
[Authorize(AuthenticationSchemes=IntegrationKeyAuthenticationHandler.SchemeName)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class ExternalOrdersController(IntegrationKeyService keys,IDeliveryOrderService orders,ITrackingService tracking,AppDbContext db):ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreatedExternalOrder),201)]
    public async Task<ActionResult<CreatedExternalOrder>> Create(CreateExternalOrderRequest request,CancellationToken ct)
    {
        var customer=await CustomerAsync(IntegrationKeyService.CreateScope,ct);
        var order=await orders.CreateAsync(customer,request.ToInternal());
        return Created($"/api/v1/delivery-orders/{order.Id}",new CreatedExternalOrder(order.Id,order.TrackingNumber,order.Status,order.PickupSecureCode,order.CreatedAt));
    }
    [HttpGet("{id}")]
    public async Task<ActionResult<ExternalOrder>> Read(string id,CancellationToken ct)
    {
        var customer=await CustomerAsync(IntegrationKeyService.ReadScope,ct); var orderId=OrderId(id);
        var order=await db.DeliveryOrders.AsNoTracking().Where(o=>o.Id==orderId && o.CustomerId==customer)
            .Select(o=>new ExternalOrder(o.Id,o.TrackingNumber,o.Status,o.PackageId,o.OriginStationId,o.DestinationStationId,o.CreatedAt,o.UpdatedAt,o.EstimatedDeliveryTime,o.ActualDeliveryTime)).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(404,"Delivery order not found.");
        return order with {CreatedAt=Utc(order.CreatedAt),UpdatedAt=Utc(order.UpdatedAt),EstimatedDeliveryTime=order.EstimatedDeliveryTime is {} eta?Utc(eta):null,ActualDeliveryTime=order.ActualDeliveryTime is {} actual?Utc(actual):null};
    }
    [HttpGet("{id}/tracking")]
    public async Task<ActionResult<TrackingSnapshot>> Tracking(string id,CancellationToken ct)
    {
        var customer=await CustomerAsync(IntegrationKeyService.ReadScope,ct);
        return (await tracking.GetForCustomerAsync(customer,OrderId(id),ct)).Snapshot;
    }
    private Task<Guid> CustomerAsync(string scope,CancellationToken ct)=>keys.RequireAccessAsync(Guid.Parse(User.FindFirst(IntegrationKeyAuthenticationHandler.KeyIdClaim)!.Value),scope,ct);
    private static Guid OrderId(string id)=>Guid.TryParse(id,out var order)?order:throw new ApiException(400,"A valid order UUID is required.");
    private static DateTime Utc(DateTime dt)=>DateTime.SpecifyKind(dt,DateTimeKind.Utc);
}
