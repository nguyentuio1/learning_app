using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Route("api/delivery-orders")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class DeliveryEtaController(DeliveryEtaQueryService estimates) : ControllerBase
{
    [HttpGet("{id:guid}/eta")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<DeliveryEtaDto>> Get(Guid id, CancellationToken ct)
    {
        var userId = Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var parsed)
            ? parsed : throw new ApiException(401, "Missing, invalid or expired token.");
        return Ok(await estimates.GetAsync(userId, id, ct));
    }
}
