using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Route("api/admin/integration-keys")]
[Authorize(Roles=AppRoles.Admin)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class IntegrationKeysController(IntegrationKeyService keys):ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreatedIntegrationKey),201)]
    public async Task<ActionResult<CreatedIntegrationKey>> Create(CreateIntegrationKeyRequest request,CancellationToken ct)
    {
        var key=await keys.CreateAsync(AdminId(),request,ct);
        return StatusCode(201,key);
    }
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IntegrationKeyMetadata>>> List([FromQuery]Guid? customerId,CancellationToken ct)
        => Ok(await keys.ListAsync(AdminId(),customerId,ct));
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Revoke(string id,CancellationToken ct)
    {
        if(!Guid.TryParse(id,out var key)) throw new ApiException(400,"A valid key UUID is required.");
        await keys.RevokeAsync(AdminId(),key,ct); return NoContent();
    }
    private Guid AdminId()=>Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,out var id)?id:throw new ApiException(401,"Missing or invalid token.");
}
