using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Tests;

/// <summary>Test-only endpoints that show how each role attribute behaves. Not part of the production API.</summary>
[ApiController]
[Route("probe")]
public class RbacProbeController : ControllerBase
{
    [HttpGet("open"), AllowAnonymous]
    public IActionResult Open() => Ok();

    [HttpGet("signed-in"), Authorize]
    public IActionResult SignedIn() => Ok();

    // No attribute on purpose: the fallback policy must still require a signed-in user.
    [HttpGet("no-attribute")]
    public IActionResult NoAttribute() => Ok();

    [HttpGet("customer"), Authorize(Roles = AppRoles.Customer)]
    public IActionResult Customer() => Ok();

    [HttpGet("dispatcher"), Authorize(Roles = AppRoles.Dispatcher)]
    public IActionResult Dispatcher() => Ok();

    [HttpGet("station-operator"), Authorize(Roles = AppRoles.StationOperator)]
    public IActionResult StationOperator() => Ok();

    [HttpGet("manager"), Authorize(Roles = AppRoles.Manager)]
    public IActionResult Manager() => Ok();

    [HttpGet("admin"), Authorize(Roles = AppRoles.Admin)]
    public IActionResult Admin() => Ok();

    [HttpGet("dispatcher-or-admin"), Authorize(Roles = AppRoles.Dispatcher + "," + AppRoles.Admin)]
    public IActionResult DispatcherOrAdmin() => Ok();
}
