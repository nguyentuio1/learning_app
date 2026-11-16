using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

internal static class PickupResponses
{
    public static IActionResult Create(ControllerBase controller, PickupVerificationResult result)
    {
        if (result.Success) return controller.Ok(result.Response);
        if (result.StatusCode == StatusCodes.Status423Locked)
        {
            if (result.RetryAfterSeconds is { } seconds)
                controller.Response.Headers["Retry-After"] = seconds.ToString(CultureInfo.InvariantCulture);
            return controller.StatusCode(423, new {
                type = "https://httpstatuses.com/423", title = "Locked", status = 423,
                detail = result.Detail, lockoutExpiresAt = result.LockoutExpiresAt
            });
        }
        return controller.StatusCode(result.StatusCode, new {
            type = $"https://httpstatuses.com/{result.StatusCode}", title = "Bad Request",
            status = result.StatusCode, detail = result.Detail, attemptsRemaining = result.AttemptsRemaining
        });
    }
}
