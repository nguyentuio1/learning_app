using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Roles = AppRoles.Manager)]
public class AnalyticsController(AnalyticsQueryService analytics) : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<DashboardResult> Dashboard(CancellationToken ct) => analytics.DashboardAsync(CurrentUser(), AnalyticsQuery.Parse(Request.Query, false), ct);

    [HttpGet("reports/deliveries")]
    public Task<DeliveryReport> Report(CancellationToken ct) => analytics.ReportAsync(CurrentUser(), AnalyticsQuery.Parse(Request.Query, true), ct);

    private Guid CurrentUser() => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id : throw new ApiException(401, "Missing, invalid or expired token.");
}
