using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Api.Services;

public class EtaPredictionWorker(IServiceScopeFactory scopes, IOptions<EtaPredictionOptions> options,
    TimeProvider clock, ILogger<EtaPredictionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        var offset = 0;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IntervalSeconds), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                using var scope = scopes.CreateScope();
                var model = scope.ServiceProvider.GetRequiredService<IEtaModelGateway>();
                if (!model.IsConfigured) continue;
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var ids = await db.DeliveryOrders.AsNoTracking()
                        .Where(o => (o.Status == "PENDING" || o.Status == "APPROVED")
                            && o.ScheduledDepartureTime == null && !o.FlightMissions.Any())
                        .OrderBy(o => o.CreatedAt).ThenBy(o => o.Id)
                        .Skip(offset).Take(options.Value.BatchSize).Select(o => o.Id).ToListAsync(stoppingToken);
                    // Visit later pages even when the first page already has cached predictions.
                    offset = ids.Count == options.Value.BatchSize && offset <= int.MaxValue - options.Value.BatchSize
                        ? offset + options.Value.BatchSize : 0;
                    foreach (var id in ids)
                    {
                        using var orderScope = scopes.CreateScope();
                        await orderScope.ServiceProvider.GetRequiredService<EtaPredictionService>().RecalculateAsync(id, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception)
                {
                    // Provider exceptions can contain payloads; never write their contents to logs.
                    logger.LogError("ETA prediction batch failed; retrying at the next interval.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
