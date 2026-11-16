using SmartDroneDelivery.Api.DTOs;

namespace SmartDroneDelivery.Api.Services;

public class DeliverySummaryService(DeliverySummaryFacts facts, ISummaryModelGateway model,
    AiGenerationSlots slots, TimeProvider clock)
{
    public async Task<DeliverySummaryDto> GenerateAsync(Guid userId, Guid orderId, CancellationToken ct)
    {
        var snapshot = await facts.ReadAsync(userId, orderId, ct);
        if (snapshot.Facts.Events.Length == 0) return new(orderId, "NO_HISTORY", null, null);
        GroundedSummaryRenderer.ValidateFacts(snapshot.Facts);
        if (!model.IsConfigured) throw new ApiException(503, "Delivery summarization is not configured.");
        using var lease = slots.TryAcquire(userId) ?? throw new ApiException(429, "Another AI request is in progress.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try
        {
            var selected = await model.SelectEventsAsync(snapshot.Facts, linked.Token).WaitAsync(linked.Token);
            ct.ThrowIfCancellationRequested();
            var current = await facts.ReadAsync(userId, orderId, linked.Token);
            if (current.Stamp != snapshot.Stamp) throw new ApiException(409, "Delivery history changed. Request a new summary.");
            var text = GroundedSummaryRenderer.Render(snapshot.Facts, selected);
            linked.Token.ThrowIfCancellationRequested();
            return new(orderId, "GENERATED", text, clock.GetUtcNow().UtcDateTime);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        { throw new ApiException(504, "Delivery summarization timed out."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new ApiException(503, "Delivery summarization is unavailable."); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or System.Text.Json.JsonException)
        { throw new ApiException(503, "Delivery summarization is unavailable."); }
    }
}
