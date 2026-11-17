using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Api.Services;

public class AssistantRetentionWorker(IServiceScopeFactory scopes, IOptions<CustomerAssistantOptions> options,
    TimeProvider clock, ILogger<AssistantRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.Value.HasApprovedRetention)
            {
                try
                {
                    int removed;
                    do
                    {
                        using var scope = scopes.CreateScope();
                        removed = await scope.ServiceProvider.GetRequiredService<AssistantRetentionService>().CleanupBatchAsync(stoppingToken);
                        await Task.Yield();
                    } while (removed == 100 && !stoppingToken.IsCancellationRequested);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception) { logger.LogWarning("Assistant retention cleanup failed; it will retry on the next scheduled run."); }
            }
            try { await Task.Delay(TimeSpan.FromHours(1), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
