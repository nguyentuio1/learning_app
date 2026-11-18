using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Route("api/delivery-insights/orders")]
[Authorize(Roles = AppRoles.Dispatcher + "," + AppRoles.Manager)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DeliveryInsightLookupController(DeliveryInsightLookupService lookup) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DeliveryInsightOrderDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<DeliveryInsightOrderDto>> List(CancellationToken ct)
    {
        var caller = Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id : throw new ApiException(401, "Missing, invalid or expired token.");
        return lookup.ListAsync(caller, Request.Query, ct);
    }
}
