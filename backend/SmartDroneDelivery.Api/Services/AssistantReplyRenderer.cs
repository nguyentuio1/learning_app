using System.Globalization;
using System.Text;

namespace SmartDroneDelivery.Api.Services;

public static class AssistantReplyRenderer
{
    private static string Status(string value) => value switch
    {
        "PENDING" => "pending review", "APPROVED" => "approved", "READY_FOR_TAKEOFF" => "ready for takeoff",
        "IN_TRANSIT" => "in transit", "ARRIVED" => "arrived at the destination station and awaiting pickup",
        "DELIVERED" => "receipt confirmed", "FAILED" => "failed", "CANCELLED" => "cancelled",
        _ => throw new ApiException(503, "The recorded delivery status is unavailable.")
    };
    private static string Time(DateTime time) => time.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
    public static string Render(AssistantIntent intent, AssistantSnapshot facts, DateTime now)
    {
        var status = "The current recorded delivery status is " + Status(facts.Status) + ".";
        var text = intent switch
        {
            AssistantIntent.Status => status,
            AssistantIntent.Eta when facts.Status is "ARRIVED" or "DELIVERED" or "FAILED" or "CANCELLED" => status + " An arrival countdown is no longer available.",
            AssistantIntent.Eta when facts.EstimatedAt is null => status + " No arrival estimate has been recorded.",
            AssistantIntent.Eta when facts.EstimatedAt <= now => status + " The recorded arrival estimate has passed; a revised estimate is not available.",
            AssistantIntent.Eta => status + " The order's recorded arrival estimate is " + Time(facts.EstimatedAt!.Value) + ". It is an estimate and may change.",
            AssistantIntent.History => status + " " + (facts.Events.Length == 0 ? "No status history has been recorded."
                : "Recent recorded events: " + string.Join(" ", facts.Events.Select(e => Time(e.CreatedAt) + ": " + Status(e.ToStatus) + "."))),
            _ => throw new ApiException(503, "The assistant could not generate a grounded answer.")
        };
        if (text.EnumerateRunes().Count() > 4000) throw new ApiException(503, "The answer exceeds the supported limit.");
        return text;
    }
}
