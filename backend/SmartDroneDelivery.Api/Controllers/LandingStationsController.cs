using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;

namespace SmartDroneDelivery.Api.Controllers;

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
    public async Task<ActionResult<IReadOnlyList<StationSummaryDto>>> List([FromQuery] string? status = null)
    {
        var query = _db.LandingStations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToUpperInvariant();
            query = query.Where(s => s.Status == normalized);
        }

        var stations = await query
            .OrderBy(s => s.Name)
            .Select(s => new StationSummaryDto(
                s.Id,
                s.StationCode,
                s.Name,
                s.Address,
                s.Latitude,
                s.Longitude,
                s.Status))
            .ToListAsync();

        return Ok(stations);
    }
}
