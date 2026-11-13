using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Api.Services;

public record RouteEstimate(decimal DistanceKm, int FlightMinutes);

public class DeliveryRouteEstimate(AppDbContext db, IOptions<DeliveryFlightOptions> options)
{
    public async Task<RouteEstimate> GetAsync(DeliveryOrder order, CancellationToken ct = default)
    {
        // No AI producer exists yet. A positive stored estimate is authoritative until
        // route edits explicitly invalidate it; approval/scheduling timestamps do not.
        var prediction = await db.AiEtaPredictions.AsNoTracking().SingleOrDefaultAsync(p => p.OrderId == order.Id, ct);
        if (prediction is { CalculatedDistanceKm: > 0, PredictedDurationMinutes: > 0 })
            return new(prediction.CalculatedDistanceKm, prediction.PredictedDurationMinutes);
        var stations = await db.LandingStations.AsNoTracking()
            .Where(s => s.Id == order.OriginStationId || s.Id == order.DestinationStationId).ToListAsync(ct);
        var origin = stations.Single(s => s.Id == order.OriginStationId);
        var destination = stations.Single(s => s.Id == order.DestinationStationId);
        var km = DistanceKm(origin.Latitude, origin.Longitude, destination.Latitude, destination.Longitude);
        return new((decimal)km, Math.Max(1, checked((int)Math.Ceiling(km * 1000 / options.Value.CruiseSpeedMps / 60))));
    }

    public static double DistanceKm(decimal fromLatitude, decimal fromLongitude, decimal toLatitude, decimal toLongitude)
    {
        const double radians = Math.PI / 180;
        var lat1 = (double)fromLatitude * radians;
        var lat2 = (double)toLatitude * radians;
        var dlat = lat2 - lat1;
        var dlon = (double)(toLongitude - fromLongitude) * radians;
        var a = Math.Pow(Math.Sin(dlat / 2), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dlon / 2), 2);
        return 6371.0088 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }
}
