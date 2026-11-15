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
    public async Task<IActionResult> Pickup(Guid stationId, ConfirmPickupRequest request) =>
        PickupResponses.Create(this, await _operations.PickupAsync(CurrentUserId(), stationId, request));

    [HttpPost("clear-expired-parcel")]
    public async Task<ActionResult<ClearExpiredParcelResponseDto>> ClearExpiredParcel(
        Guid stationId, ClearExpiredParcelRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _operations.ClearExpiredParcelAsync(CurrentUserId(), stationId, request, cancellationToken));
    }

    [HttpGet("expired-parcels")]
    public async Task<ActionResult<IReadOnlyList<ExpiredParcelDto>>> ListExpiredParcels(
        Guid stationId, CancellationToken cancellationToken)
    {
        return Ok(await _operations.ListExpiredParcelsAsync(
            CurrentUserId(), stationId, cancellationToken));
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
