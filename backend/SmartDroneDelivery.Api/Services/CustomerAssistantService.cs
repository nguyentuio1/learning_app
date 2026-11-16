using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Options;
using Microsoft.Extensions.Options;

namespace SmartDroneDelivery.Api.Services;

public class CustomerAssistantService(AppDbContext db, AssistantAccess access, TimeProvider clock,
    IOptions<CustomerAssistantOptions> options, IAssistantModelGateway model, AssistantContextReader reader,
    AssistantExchangeWriter writer, AiGenerationSlots slots)
{
    public async Task<AssistantResponseDto> SendAsync(Guid userId, AssistantMessageRequest request, CancellationToken ct)
    {
        await access.RequireCustomerAsync(userId, ct);
        var message = AssistantInput.Validate(request.Message);
        if (request.SessionId is { } sessionId) await access.RequireSessionAsync(userId, sessionId, ct);
        if (request.OrderId is { } orderId) await access.RequireOrderAsync(userId, orderId, ct);
        if (AssistantInput.IsSensitive(message)) return Static("REFUSED", "Do not send pickup codes, credentials or contact details. I can help with the status of a delivery you select.");
        if (message.Equals("help", StringComparison.OrdinalIgnoreCase) || message.Equals("trợ giúp", StringComparison.OrdinalIgnoreCase))
            return Static("HELP", "Select one of your deliveries to ask about its recorded status, estimated arrival or recent history.");
        if (request.OrderId is null)
        {
            var hasOrders = await db.DeliveryOrders.AsNoTracking().AnyAsync(o => o.CustomerId == userId, ct);
            return hasOrders ? Static("CLARIFICATION_REQUIRED", "Select the delivery you want to ask about.")
                : Static("HELP", "There are no deliveries available for your account. Create or select a delivery before asking about its status.");
        }
        if (!options.Value.Enabled || !options.Value.HasApprovedRetention || !model.IsConfigured)
            throw new ApiException(503, "The assistant is temporarily unavailable.");
        using var lease = slots.TryAcquire(userId) ?? throw new ApiException(429, "Another AI request is in progress.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try
        {
            var snapshot = await reader.ReadAsync(userId, request.SessionId, request.OrderId.Value, linked.Token);
            var intent = await model.ClassifyAsync(new(message, snapshot.ContextHistory()), linked.Token).WaitAsync(linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(intent)) throw new ApiException(503, "The assistant could not generate a grounded answer.");
            if (intent is AssistantIntent.Help or AssistantIntent.Clarify or AssistantIntent.Refuse)
            {
                var current = await reader.ReadAsync(userId, request.SessionId, request.OrderId.Value, linked.Token);
                if (current.Stamp != snapshot.Stamp) throw new ApiException(409, "The conversation or delivery changed. Refresh before trying again.");
                return intent switch
                {
                    AssistantIntent.Help => Static("HELP", "I can help with the recorded status, arrival estimate or recent history of the delivery you select."),
                    AssistantIntent.Clarify => Static("CLARIFICATION_REQUIRED", "Ask about the selected delivery's status, arrival estimate or recent history."),
                    _ => Static("REFUSED", "I can only answer supported questions about your selected delivery. I cannot access other customers' data or change deliveries.")
                };
            }
            return await writer.CommitAsync(userId, snapshot, message, intent, linked.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        { throw new ApiException(504, "The assistant request timed out."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new ApiException(503, "The assistant is temporarily unavailable."); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or System.Text.Json.JsonException or DbUpdateException or System.Data.Common.DbException)
        { throw new ApiException(503, "The assistant is temporarily unavailable."); }
    }
    private AssistantResponseDto Static(string outcome, string text) => new(outcome, null, null, new(null, "BOT", text, clock.GetUtcNow().UtcDateTime));
}
