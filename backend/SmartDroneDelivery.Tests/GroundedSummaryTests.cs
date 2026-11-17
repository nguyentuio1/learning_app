using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class GroundedSummaryTests
{
    [Fact]
    public void Real_ready_for_takeoff_lifecycle_is_supported()
    {
        var facts = new SummaryModelInput("READY_FOR_TAKEOFF", [new(null, "PENDING", DateTime.UnixEpoch),
            new("PENDING", "APPROVED", DateTime.UnixEpoch.AddMinutes(1)),
            new("APPROVED", "READY_FOR_TAKEOFF", DateTime.UnixEpoch.AddMinutes(2))]);
        Assert.Contains("ready for takeoff", GroundedSummaryRenderer.Render(facts, [0, 1, 2]));
    }

    [Fact]
    public void Rescheduling_same_status_event_cannot_be_silently_omitted()
    {
        var facts = new SummaryModelInput("IN_TRANSIT", [new(null, "PENDING", DateTime.UnixEpoch),
            new("READY_FOR_TAKEOFF", "READY_FOR_TAKEOFF", DateTime.UnixEpoch.AddMinutes(1)),
            new("READY_FOR_TAKEOFF", "IN_TRANSIT", DateTime.UnixEpoch.AddMinutes(2))]);
        var error = Assert.Throws<ApiException>(() => GroundedSummaryRenderer.Render(facts, [0, 2]));
        Assert.Equal(503, error.StatusCode);
    }

    [Fact]
    public void Excessive_required_history_is_rejected_rather_than_truncated()
    {
        var facts = new SummaryModelInput("FAILED", Enumerable.Range(0, 200)
            .Select(i => new SummaryEvent("IN_TRANSIT", "FAILED", DateTime.UnixEpoch.AddMinutes(i))).ToArray());
        Assert.Equal(503, Assert.Throws<ApiException>(() => GroundedSummaryRenderer.Render(facts, Enumerable.Range(0, 200).ToArray())).StatusCode);
    }
}
