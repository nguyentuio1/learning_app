using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

public interface IStationAvailabilityService
{
    Task<StationAvailabilityDto> GetAvailabilityAsync(Guid userId, Guid stationId);
}

public class StationAvailabilityService : IStationAvailabilityService
{
    private readonly AppDbContext _db;

    public StationAvailabilityService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<StationAvailabilityDto> GetAvailabilityAsync(Guid userId, Guid stationId)
    {
        await LoadActiveUserAsync(userId);

        var station = await _db.LandingStations
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == stationId)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Landing station not found.");

        var slots = await _db.StationSlots
            .AsNoTracking()
            .Where(s => s.StationId == stationId)
            .ToListAsync();

        var emptySlots = slots.Count(s => s.Status == "EMPTY");
        var occupiedSlots = slots.Count(s => s.Status == "OCCUPIED");
        var reservedSlots = slots.Count(s => s.Status == "RESERVED");
        var errorSlots = slots.Count(s => s.Status == "ERROR");
        var totalSlots = emptySlots + occupiedSlots + reservedSlots + errorSlots;

        var isAvailable = station.Status == "OPERATIONAL" && emptySlots >= 1;

        return new StationAvailabilityDto(
            station.Id,
            station.Status,
            totalSlots,
            emptySlots,
            occupiedSlots,
            reservedSlots,
            errorSlots,
            isAvailable,
            isAvailable,
            DateTime.UtcNow);
    }

    private async Task<User> LoadActiveUserAsync(Guid userId)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");

        if (user.Status != "ACTIVE")
            throw new ApiException(StatusCodes.Status403Forbidden, "This account is not active.");

        return user;
    }
}
