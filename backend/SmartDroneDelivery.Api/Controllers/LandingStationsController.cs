using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;

namespace SmartDroneDelivery.Api.Controllers;

/// <summary>Read-only station list for the order screens (contract section 4). Full station management is Sprint 4.</summary>
[ApiController]
[Route("api/landing-stations")]
[Authorize]
public class LandingStationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public LandingStationsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StationSummaryDto>>> List([FromQuery] string? status)
    {
        var query = _db.LandingStations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = status.Trim().ToUpperInvariant();
            query = query.Where(s => s.Status == wanted);
        }

        var stations = await query.OrderBy(s => s.Name).ToListAsync();
        return stations.Select(StationSummaryDto.From).ToList();
    }
}
