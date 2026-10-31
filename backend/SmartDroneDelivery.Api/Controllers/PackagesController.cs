using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Route("api/packages")]
[Authorize(Roles = AppRoles.Customer)]
public class PackagesController : ControllerBase
{
    private readonly IPackageService _packageService;

    public PackagesController(IPackageService packageService)
    {
        _packageService = packageService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PackageDto>>> List()
    {
        return Ok(await _packageService.ListAsync(CurrentUserId()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PackageDto>> GetById(Guid id)
    {
        return Ok(await _packageService.GetByIdAsync(id, CurrentUserId(), CurrentUserRole()));
    }

    [HttpPost]
    public async Task<ActionResult<PackageDto>> Create([FromBody] CreatePackageRequest request)
    {
        var created = await _packageService.CreateAsync(CurrentUserId(), request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PackageDto>> Update(Guid id, [FromBody] UpdatePackageRequest request)
    {
        return Ok(await _packageService.UpdateAsync(id, CurrentUserId(), CurrentUserRole(), request));
    }

    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PackageImageResponse>> UploadImage(Guid id, IFormFile? file)
    {
        return Ok(await _packageService.UploadImageAsync(id, CurrentUserId(), CurrentUserRole(), file));
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");

    private string CurrentUserRole() =>
        User.FindFirst("role")?.Value ?? AppRoles.Customer;
}
