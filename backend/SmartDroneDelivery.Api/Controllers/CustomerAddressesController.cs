using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Route("api/customer-addresses")]
[Authorize(Roles = AppRoles.Customer)]
public class CustomerAddressesController : ControllerBase
{
    private readonly ICustomerAddressService _addressService;

    public CustomerAddressesController(ICustomerAddressService addressService)
    {
        _addressService = addressService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerAddressDto>>> List()
    {
        return Ok(await _addressService.ListAsync(CurrentUserId()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerAddressDto>> GetById(Guid id)
    {
        return Ok(await _addressService.GetByIdAsync(id, CurrentUserId(), CurrentUserRole()));
    }

    [HttpPost]
    public async Task<ActionResult<CustomerAddressDto>> Create([FromBody] CreateCustomerAddressRequest request)
    {
        var created = await _addressService.CreateAsync(CurrentUserId(), request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerAddressDto>> Update(Guid id, [FromBody] UpdateCustomerAddressRequest request)
    {
        return Ok(await _addressService.UpdateAsync(id, CurrentUserId(), CurrentUserRole(), request));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _addressService.DeleteAsync(id, CurrentUserId(), CurrentUserRole());
        return NoContent();
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");

    private string CurrentUserRole() =>
        User.FindFirst("role")?.Value ?? AppRoles.Customer;
}
