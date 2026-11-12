using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public interface IFlightTelemetryService
{
    Task IngestForAdminAsync(Guid userId, Guid missionId, TelemetrySampleRequest request, CancellationToken ct = default);
    // Trusted internal simulator entry point. Not exposed as a client-accessible route.
    Task IngestAsync(Guid missionId, TelemetrySampleRequest request, CancellationToken ct = default);
    // Callers exposing this read model must authorize the owning order before returning data.
    Task<MissionTelemetrySnapshot> GetLatestAsync(Guid missionId, CancellationToken ct = default);
}

public class FlightTelemetryService(AppDbContext db, TimeProvider clock) : IFlightTelemetryService
{
    private static readonly Regex UtcTimestamp = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|\+00:00)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task IngestForAdminAsync(Guid userId, Guid missionId, TelemetrySampleRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.Status, Role = u.Role.Code }).FirstOrDefaultAsync(ct)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");
        if (user.Status != "ACTIVE" || user.Role != AppRoles.Admin)
            throw new ApiException(StatusCodes.Status403Forbidden, "An active administrator account is required.");
        await IngestAsync(missionId, request, ct);
    }

    public async Task IngestAsync(Guid missionId, TelemetrySampleRequest request, CancellationToken ct = default)
    {
        var timestamp = Validate(request);
        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct) : null;
        if (db.Database.IsRelational())
        {
            // Serialize with a landing/abort that updates this mission. Re-read its status
            // after acquiring the lock, never from a potentially stale tracked entity.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM flight_missions WHERE \"Id\" = {missionId} FOR UPDATE", ct);
        }
        var status = await db.FlightMissions.AsNoTracking().Where(m => m.Id == missionId)
            .Select(m => m.FlightStatus).FirstOrDefaultAsync(ct)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Flight mission not found.");
        if (status != "IN_AIR")
            throw new ApiException(StatusCodes.Status409Conflict, "Telemetry requires an IN_AIR mission.");

        db.FlightTelemetryLogs.Add(new FlightTelemetryLog
        {
            MissionId = missionId,
            Latitude = request.Latitude!.Value,
            Longitude = request.Longitude!.Value,
            AltitudeMeters = request.AltitudeMeters!.Value,
            SpeedMps = request.SpeedMps!.Value,
            BatteryPercentage = request.BatteryPercentage!.Value,
            Timestamp = timestamp
        });
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    public async Task<MissionTelemetrySnapshot> GetLatestAsync(Guid missionId, CancellationToken ct = default)
    {
        // One projection keeps mission state and its latest sample in the same read snapshot.
        var result = await db.FlightMissions.AsNoTracking().Where(m => m.Id == missionId)
            .Select(m => new
            {
                m.FlightStatus,
                Sample = m.TelemetryLogs.OrderByDescending(l => l.Timestamp).ThenByDescending(l => l.Id).FirstOrDefault()
            }).FirstOrDefaultAsync(ct)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Flight mission not found.");
        var sample = result.Sample;
        var lastAt = sample is null ? (DateTime?)null : DateTime.SpecifyKind(sample.Timestamp, DateTimeKind.Utc);
        if (result.FlightStatus != "IN_AIR")
            return new(TelemetryAvailability.NotInAir, lastAt, null);
        if (sample is null)
            return new(TelemetryAvailability.NoSample, null, null);

        var stale = clock.GetUtcNow().UtcDateTime - lastAt!.Value > TimeSpan.FromSeconds(60);
        return new(stale ? TelemetryAvailability.Stale : TelemetryAvailability.Fresh, lastAt,
            new(sample.Latitude, sample.Longitude, sample.AltitudeMeters, sample.SpeedMps, sample.BatteryPercentage, lastAt.Value));
    }

    private DateTime Validate(TelemetrySampleRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        CheckRange(request.Latitude, -90, 90, "latitude");
        CheckRange(request.Longitude, -180, 180, "longitude");
        // Existing PostgreSQL columns are numeric(6,2) and numeric(5,2).
        CheckRange(request.AltitudeMeters, 0, 9999.99m, "altitudeMeters");
        CheckRange(request.SpeedMps, 0, 999.99m, "speedMps");
        CheckRange(request.BatteryPercentage, 0, 100, "batteryPercentage");
        DateTimeOffset timestamp = default;
        if (request.Timestamp is null || !UtcTimestamp.IsMatch(request.Timestamp) ||
            !DateTimeOffset.TryParse(request.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp) ||
            timestamp.Offset != TimeSpan.Zero || timestamp > clock.GetUtcNow())
            errors["timestamp"] = ["Timestamp must be ISO 8601 UTC (Z or +00:00) and not in the future."];
        if (errors.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "One or more fields are invalid.", errors);
        return timestamp.UtcDateTime;

        void CheckRange(decimal? value, decimal minimum, decimal maximum, string field)
        {
            if (!value.HasValue || value.Value < minimum || value.Value > maximum)
                errors[field] = [$"A value from {minimum.ToString(CultureInfo.InvariantCulture)} to {maximum.ToString(CultureInfo.InvariantCulture)} is required."];
        }
    }
}
