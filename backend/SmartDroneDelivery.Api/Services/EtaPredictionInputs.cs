using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Services;

internal sealed record EtaPredictionSource(Guid OrderId, Guid PackageId, Guid OriginId, Guid DestinationId,
    string Status, DateTime UpdatedAt, DateTime? ScheduledDeparture, bool HasMission,
    EtaModelInput Input, string Fingerprint, AiEtaPrediction? Prediction, string PredictionVersion)
{
    public bool Eligible => Status is "PENDING" or "APPROVED" && ScheduledDeparture is null && !HasMission
        && Input.DistanceKm is > 0 and <= 9999.99m && Input.WeightGram is > 0 and <= 3000
        && double.IsFinite(Input.CruiseSpeedMps) && Input.CruiseSpeedMps is >= 0.001 and <= 999.99;

    public bool PlausibleDuration(int minutes) => minutes is >= 1 and <= 1440
        && minutes >= Math.Ceiling((double)Input.DistanceKm * 1000 / Input.CruiseSpeedMps / 60);
}

internal static class EtaPredictionInputs
{
    public static async Task<EtaPredictionSource?> ReadAsync(AppDbContext db, Guid orderId, double speed, CancellationToken ct)
    {
        var row = await db.DeliveryOrders.AsNoTracking().Where(o => o.Id == orderId)
            .Select(o => new { o.Id, o.PackageId, o.OriginStationId, o.DestinationStationId,
                o.Status, o.UpdatedAt, o.ScheduledDepartureTime, HasMission = o.FlightMissions.Any(),
                o.Package.WeightGram, o.Package.IsFragile,
                FromLat = o.OriginStation.Latitude, FromLon = o.OriginStation.Longitude,
                ToLat = o.DestinationStation.Latitude, ToLon = o.DestinationStation.Longitude, o.AiEtaPrediction })
            .SingleOrDefaultAsync(ct);
        if (row is null) return null;
        var validCoordinates = row.FromLat is >= -90 and <= 90 && row.ToLat is >= -90 and <= 90
            && row.FromLon is >= -180 and <= 180 && row.ToLon is >= -180 and <= 180;
        var input = new EtaModelInput(validCoordinates ? Math.Round((decimal)DeliveryRouteEstimate.DistanceKm(
            row.FromLat, row.FromLon, row.ToLat, row.ToLon), 2, MidpointRounding.AwayFromZero) : 0,
            row.WeightGram, row.IsFragile, speed, null);
        static string Number(decimal number) => number.ToString("G29", CultureInfo.InvariantCulture);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|',
            "full-route-v1", row.PackageId.ToString("N"), row.OriginStationId.ToString("N"), row.DestinationStationId.ToString("N"),
            Number(row.FromLat), Number(row.FromLon), Number(row.ToLat), Number(row.ToLon),
            Number(row.WeightGram), row.IsFragile ? "fragile" : "ordinary", speed.ToString("R", CultureInfo.InvariantCulture)))));
        return new(row.Id, row.PackageId, row.OriginStationId, row.DestinationStationId, row.Status,
            row.UpdatedAt, row.ScheduledDepartureTime, row.HasMission, input, fingerprint, row.AiEtaPrediction,
            JsonSerializer.Serialize(row.AiEtaPrediction is { } p ? new {
                p.Id, p.CreatedAt, p.InputFingerprint, p.ModelVersion, p.CalculatedDistanceKm,
                p.PredictedDurationMinutes, p.WeatherCondition, p.ConfidenceScore } : null));
    }

    public static bool Matches(EtaPredictionSource source, AiEtaPrediction prediction) =>
        prediction.InputFingerprint == source.Fingerprint && !string.IsNullOrWhiteSpace(prediction.ModelVersion)
        && prediction.CalculatedDistanceKm == source.Input.DistanceKm
        && source.PlausibleDuration(prediction.PredictedDurationMinutes);
}
