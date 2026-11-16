namespace SmartDroneDelivery.Api.Services;

public interface ISummaryModelGateway
{
    bool IsConfigured { get; }
    Task<int[]> SelectEventsAsync(SummaryModelInput input, CancellationToken ct);
}
