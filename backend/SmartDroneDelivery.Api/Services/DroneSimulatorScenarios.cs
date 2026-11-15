using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

public partial class DroneSimulatorService
{
    private bool IsTelemetryLost(double elapsed) => options.Value.TelemetryLossAfterSeconds is { } start
        && options.Value.TelemetryLossDurationSeconds is { } duration && elapsed >= start && elapsed < start + duration;

    private async Task AbortAsync(DeliveryOrder order, FlightMission mission, List<LandingStation> stations,
        (decimal Latitude, decimal Longitude) position, DateTime now, CancellationToken ct)
    {
        // Staging joins our operation gate and transaction; it neither saves nor frees a flying drone.
        await new FailureService(db, transitions, clock).StageInAirFailureAsync(order, "MISSION_ABORTED",
            "Configured simulator mission abort", null, "SIMULATOR_ABORT", now, ct);
        var diversion = new SimulatorDiversion {
            MissionId = mission.Id, AbortedAt = now, AbortLatitude = position.Latitude,
            AbortLongitude = position.Longitude, CruiseSpeedMps = flight.Value.CruiseSpeedMps
        };
        SelectLandingStation(diversion, stations, now);
        db.SimulatorDiversions.Add(diversion);
    }

    private static void SelectLandingStation(SimulatorDiversion diversion, List<LandingStation> stations, DateTime now)
    {
        var nearest = stations.Where(s => s.Status == "OPERATIONAL")
            .Select(s => new { Station = s, Distance = DeliveryRouteEstimate.DistanceKm(diversion.AbortLatitude,
                diversion.AbortLongitude, s.Latitude, s.Longitude) * 1000 })
            .OrderBy(s => s.Distance).ThenBy(s => s.Station.Id).FirstOrDefault();
        if (nearest is null) return; // Explicit unresolved diversion: hover until a station can be selected.
        diversion.LandingStationId = nearest.Station.Id;
        diversion.LandingLatitude = nearest.Station.Latitude;
        diversion.LandingLongitude = nearest.Station.Longitude;
        diversion.NavigationStartedAt = now;
        // Round UP to persisted timestamp precision: a landing can never be reported early.
        var travelTicks = Math.Ceiling(nearest.Distance / diversion.CruiseSpeedMps * TimeSpan.TicksPerSecond / 10) * 10;
        // Very small but valid configured speeds can exceed DateTime's range. Keep unresolved rather than overflow.
        if (!double.IsFinite(travelTicks) || travelTicks > DateTime.MaxValue.Ticks - now.Ticks) return;
        diversion.LandingDueAt = now.AddTicks((long)travelTicks);
    }

    private async Task RecoverAsync(FlightMission mission, List<LandingStation> stations, DateTime now, CancellationToken ct)
    {
        var diversion = await db.SimulatorDiversions.SingleAsync(d => d.MissionId == mission.Id, ct);
        await db.Entry(diversion).ReloadAsync(ct);
        if (mission.FlightStatus != "ABORTED" || diversion.LandedAt is not null || now <= diversion.AbortedAt) return;
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM drones WHERE \"Id\" = {mission.DroneId} FOR UPDATE", ct);
        var drone = await db.Drones.SingleAsync(d => d.Id == mission.DroneId, ct);
        await db.Entry(drone).ReloadAsync(ct);
        // Never overwrite a newer mission or an externally reconciled physical state.
        if (drone.Status != "FLYING" || drone.CurrentStationId is not null
            || await db.FlightMissions.AnyAsync(m => m.Id != mission.Id && m.DroneId == drone.Id
                && (m.FlightStatus == "PLANNED" || m.FlightStatus == "IN_AIR" || m.StartTime > mission.StartTime), ct)) return;
        if (diversion.LandingStationId is null) { SelectLandingStation(diversion, stations, now); return; }
        var station = stations.SingleOrDefault(s => s.Id == diversion.LandingStationId);
        if (station is null || station.Status != "OPERATIONAL" || station.Latitude != diversion.LandingLatitude
            || station.Longitude != diversion.LandingLongitude || diversion.LandingDueAt is null
            || now < diversion.LandingDueAt || now <= diversion.NavigationStartedAt) return;
        diversion.LandedAt = now;
        drone.CurrentStationId = station.Id;
        drone.Status = "CHARGING";
        // No parcel slot, delivery timestamp, receipt, or failure resolution is implied by drone landing.
    }
}
