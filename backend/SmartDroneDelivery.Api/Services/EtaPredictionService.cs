using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Api.Services;

public record EtaModelInput(decimal DistanceKm, decimal WeightGram, bool IsFragile,
    double CruiseSpeedMps, string? WeatherCondition);
public interface IEtaModelGateway
{
    bool IsConfigured { get; }
    string ModelVersion { get; }
    Task<int> PredictDurationAsync(EtaModelInput input, CancellationToken ct);
}
public enum EtaPredictionOutcome { Generated, Unchanged, Ineligible, Superseded, Unavailable }

public class EtaPredictionService(AppDbContext db, IEtaModelGateway model, IOrderTransitionWriter operations,
    IOptions<DeliveryFlightOptions> flight, TimeProvider clock)
{
    public async Task<EtaPredictionOutcome> RecalculateAsync(Guid orderId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("ETA production requires a clean unit of work.");
        if (!model.IsConfigured) return EtaPredictionOutcome.Unavailable;
        var source = await EtaPredictionInputs.ReadAsync(db, orderId, flight.Value.CruiseSpeedMps, ct);
        if (source is null || !source.Eligible) return EtaPredictionOutcome.Ineligible;
        var modelVersion = model.ModelVersion;
        if (source.Prediction is { } cached && EtaPredictionInputs.Matches(source, cached)
            && cached.ModelVersion == modelVersion) return EtaPredictionOutcome.Unchanged;

        int duration;
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            try { duration = await model.PredictDurationAsync(source.Input, deadline.Token).WaitAsync(deadline.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return EtaPredictionOutcome.Unavailable; }
            catch (HttpRequestException) { return EtaPredictionOutcome.Unavailable; }
            catch (System.Text.Json.JsonException) { return EtaPredictionOutcome.Unavailable; }
        }
        if (!source.PlausibleDuration(duration)) return EtaPredictionOutcome.Unavailable;
        ct.ThrowIfCancellationRequested();

        using var operation = await operations.EnterOperationAsync(ct);
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            if (db.Database.IsNpgsql())
            {
                var locked = (await db.DeliveryOrders.FromSqlInterpolated(
                    $"SELECT * FROM delivery_orders WHERE \"Id\" = {orderId} FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();
                if (locked is null) return EtaPredictionOutcome.Superseded;
                await db.Packages.FromSqlInterpolated(
                    $"SELECT * FROM packages WHERE \"Id\" = {locked.PackageId} FOR SHARE").AsNoTracking().ToListAsync(ct);
                await db.LandingStations.FromSqlInterpolated(
                    $"SELECT * FROM landing_stations WHERE \"Id\" IN ({locked.OriginStationId}, {locked.DestinationStationId}) ORDER BY \"Id\" FOR SHARE")
                    .AsNoTracking().ToListAsync(ct);
            }
            var current = await EtaPredictionInputs.ReadAsync(db, orderId, flight.Value.CruiseSpeedMps, ct);
            if (current is null || !current.Eligible || current.Status != source.Status || current.UpdatedAt != source.UpdatedAt
                || current.Fingerprint != source.Fingerprint || current.PredictionVersion != source.PredictionVersion
                || model.ModelVersion != modelVersion)
                return EtaPredictionOutcome.Superseded;
            var prediction = await db.AiEtaPredictions.SingleOrDefaultAsync(p => p.OrderId == orderId, ct);
            if (prediction is null)
            {
                prediction = new AiEtaPrediction { OrderId = orderId };
                db.AiEtaPredictions.Add(prediction);
            }
            prediction.CalculatedDistanceKm = source.Input.DistanceKm;
            prediction.PredictedDurationMinutes = duration;
            prediction.InputFingerprint = source.Fingerprint;
            prediction.ModelVersion = modelVersion;
            prediction.WeatherCondition = null;
            prediction.ConfidenceScore = null;
            prediction.CreatedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return EtaPredictionOutcome.Generated;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
