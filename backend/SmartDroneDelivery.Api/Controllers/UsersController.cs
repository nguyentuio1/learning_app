using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

/// <summary>Profile of the signed-in user. Any role may call these endpoints.</summary>
[ApiController]
[Route("api/users/me")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<UserDto>> Get() => await _users.GetAsync(CurrentUserId());

    [HttpPut]
    public async Task<ActionResult<UserDto>> Update(UpdateProfileRequest request)
        => await _users.UpdateAsync(CurrentUserId(), request);

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        await _users.ChangePasswordAsync(CurrentUserId(), request, Client());
        return NoContent();
    }

    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AvatarResponse>> UploadAvatar(IFormFile? file)
        => await _users.UploadAvatarAsync(CurrentUserId(), file);

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");

    private ClientInfo Client() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
}
