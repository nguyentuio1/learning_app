using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

/// <summary>LA-29: Station operations (docs/api/station-api.md section 4).</summary>
[ApiController]
[Route("api/landing-stations/{stationId:guid}/operations")]
[Authorize(Roles = AppRoles.StationOperator + "," + AppRoles.Admin)]
public class StationOperationsController : ControllerBase
{
    private readonly IStationOperationService _operations;

    public StationOperationsController(IStationOperationService operations)
    {
        _operations = operations;
    }

    [HttpGet("orders/{trackingNumber}")]
    public async Task<ActionResult<StationOrderInfoDto>> GetOrder(Guid stationId, string trackingNumber)
    {
        return Ok(await _operations.GetOrderInfoAsync(CurrentUserId(), stationId, trackingNumber));
    }

    [HttpPost("intake")]
    public async Task<ActionResult<PackageIntakeResponseDto>> Intake(Guid stationId, PackageIntakeRequest request)
    {
        return Ok(await _operations.IntakeAsync(CurrentUserId(), stationId, request));
    }

    [HttpPost("intake-reject")]
    public async Task<ActionResult<RejectIntakeResponseDto>> RejectIntake(Guid stationId, RejectIntakeRequest request)
    {
        return Ok(await _operations.RejectIntakeAsync(CurrentUserId(), stationId, request));
    }

    [HttpPost("pickup")]
    public async Task<IActionResult> Pickup(Guid stationId, ConfirmPickupRequest request)
    {
        var result = await _operations.PickupAsync(CurrentUserId(), stationId, request);
        if (result.Success)
            return Ok(result.Response);

        if (result.StatusCode == StatusCodes.Status423Locked)
        {
            if (result.RetryAfterSeconds.HasValue)
                Response.Headers["Retry-After"] = result.RetryAfterSeconds.Value.ToString();

            return StatusCode(StatusCodes.Status423Locked, new
            {
                type = "https://httpstatuses.com/423",
                title = "Locked",
                status = 423,
                detail = result.Detail,
                lockoutExpiresAt = result.LockoutExpiresAt
            });
        }

        return StatusCode(result.StatusCode, new
        {
            type = $"https://httpstatuses.com/{result.StatusCode}",
            title = "Bad Request",
            status = result.StatusCode,
            detail = result.Detail,
            attemptsRemaining = result.AttemptsRemaining
        });
    }

    [HttpPost("clear-expired-parcel")]
    public async Task<ActionResult<ClearExpiredParcelResponseDto>> ClearExpiredParcel(
        Guid stationId, ClearExpiredParcelRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _operations.ClearExpiredParcelAsync(CurrentUserId(), stationId, request, cancellationToken));
    }

    [HttpPost("validate-code")]
    public async Task<ActionResult<ValidateSecureCodeResponseDto>> ValidateCode(Guid stationId, ConfirmPickupRequest request)
    {
        return Ok(await _operations.ValidateCodeAsync(CurrentUserId(), stationId, request));
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");
}
