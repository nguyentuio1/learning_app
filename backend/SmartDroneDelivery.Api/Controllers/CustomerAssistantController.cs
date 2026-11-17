using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Route("api/ai-assistant")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CustomerAssistantController(CustomerAssistantService assistant, AssistantHistoryService history, AssistantRetentionService retention) : ControllerBase
{
    [HttpPost("messages"), AssistantBodyLimit]
    public async Task<ActionResult<AssistantResponseDto>> Send(AssistantMessageRequest request, CancellationToken ct)
    {
        try { return Ok(await assistant.SendAsync(UserId(), request, ct)); }
        catch (ApiException ex) when (ex.StatusCode == 429) { Response.Headers.RetryAfter = "5"; throw; }
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<AssistantSessionsDto>> Sessions([FromQuery] AssistantPageQuery query, CancellationToken ct)
        => Ok(await history.SessionsAsync(UserId(), query, ct));

    [HttpGet("sessions/{id:guid}/messages")]
    public async Task<ActionResult<AssistantMessagesDto>> Messages(Guid id, [FromQuery] AssistantPageQuery query, CancellationToken ct)
        => Ok(await history.MessagesAsync(UserId(), id, query, ct));

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    { await retention.DeleteAsync(UserId(), id, ct); return NoContent(); }

    private Guid UserId()
    {
        Response.Headers.CacheControl = "no-store";
        return Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var parsed)
            ? parsed : throw new ApiException(401, "Missing, invalid or expired token.");
    }
}
