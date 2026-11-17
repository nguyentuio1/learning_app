using System.Globalization;
using System.Text;

namespace SmartDroneDelivery.Api.Services;

/// <summary>Model selections are untrusted. Only known factual phrases reach the client.</summary>
public static class GroundedSummaryRenderer
{
    private static readonly IReadOnlyDictionary<string, string> Phrases = new Dictionary<string, string>
    {
        ["PENDING"] = "pending review", ["APPROVED"] = "approved", ["REJECTED"] = "rejected",
        ["READY_FOR_TAKEOFF"] = "ready for takeoff", ["IN_TRANSIT"] = "in transit", ["ARRIVED"] = "arrived at the destination station and awaiting pickup",
        ["DELIVERED"] = "receipt confirmed", ["FAILED"] = "failed", ["CANCELLED"] = "cancelled"
    };

    public static void ValidateFacts(SummaryModelInput facts)
    {
        if (!Phrases.ContainsKey(facts.CurrentStatus) || facts.Events.Any(e => !Phrases.ContainsKey(e.ToStatus)
                || e.FromStatus is not null && !Phrases.ContainsKey(e.FromStatus)))
            throw Invalid();
    }

    public static int[] RequiredIndexes(SummaryModelInput facts) => Enumerable.Range(0, facts.Events.Length)
        .Where(i => i == 0 || i == facts.Events.Length - 1 || facts.Events[i].ToStatus is "FAILED" or "CANCELLED" or "ARRIVED" or "DELIVERED"
            || facts.Events[i].FromStatus is "FAILED" or "CANCELLED"
            || facts.Events[i].FromStatus == facts.Events[i].ToStatus).ToArray();

    public static string Render(SummaryModelInput facts, int[] indexes)
    {
        ValidateFacts(facts);
        if (indexes is null || indexes.Length == 0 || indexes.Length > facts.Events.Length
            || indexes.Any(i => i < 0 || i >= facts.Events.Length)
            || !indexes.SequenceEqual(indexes.Distinct().Order())
            || RequiredIndexes(facts).Except(indexes).Any()) throw Invalid();
        var text = new StringBuilder("Current recorded status: ").Append(Phrases[facts.CurrentStatus]).Append('.');
        foreach (var index in indexes)
        {
            var e = facts.Events[index];
            var phrase = e is { FromStatus: "FAILED" or "CANCELLED", ToStatus: "APPROVED" }
                ? "approved again after the earlier " + Phrases[e.FromStatus] + " status" : Phrases[e.ToStatus];
            text.Append(' ').Append(e.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture))
                .Append(": ").Append(phrase).Append('.');
        }
        var result = text.ToString();
        if (result.EnumerateRunes().Count() > 4000) throw Invalid();
        return result;
    }

    private static ApiException Invalid() => new(503, "A grounded delivery summary could not be generated.");
}
