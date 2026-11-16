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
public class DeliverySummaryController(DeliverySummaryService summaries) : ControllerBase
{
    [HttpPost("{id:guid}/ai-summary")]
    public async Task<ActionResult<DeliverySummaryDto>> Generate(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var userId = Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var parsed)
            ? parsed : throw new ApiException(401, "Missing, invalid or expired token.");
        try { return Ok(await summaries.GenerateAsync(userId, id, ct)); }
        catch (ApiException ex) when (ex.StatusCode == 429)
        { Response.Headers.RetryAfter = "5"; throw; }
    }
}
