using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace SmartDroneDelivery.Api.Services;

public class LocalSummaryModelGateway(HttpClient client, IOptions<DeliverySummaryOptions> options) : ISummaryModelGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public bool IsConfigured => options.Value.Enabled && options.Value.HasValidConfiguration();
    public async Task<int[]> SelectEventsAsync(SummaryModelInput input, CancellationToken ct)
    {
        if (!IsConfigured || !options.Value.TryGetEndpoint(out var endpoint))
            throw new HttpRequestException("Summary model is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                model = options.Value.ModelName.Trim(), stream = false,
                system = "Select important chronological delivery events from the supplied data for a short factual summary. "
                    + "Return only JSON eventIndexes, a sorted unique array of zero-based event indexes. Include all requiredIndexes. "
                    + "ARRIVED is arrival at the destination station, not recipient delivery. Do not write prose or invent reasons.",
                prompt = JsonSerializer.Serialize(new { input.CurrentStatus, input.Events,
                    requiredIndexes = GroundedSummaryRenderer.RequiredIndexes(input) }, JsonOptions),
                format = new { type = "object", required = new[] { "eventIndexes" }, additionalProperties = false,
                    properties = new { eventIndexes = new { type = "array", minItems = 1, maxItems = DeliverySummaryFacts.MaximumEvents,
                        items = new { type = "integer", minimum = 0, maximum = input.Events.Length - 1 } } } },
                options = new { temperature = 0, num_predict = 1024 }
            })
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[16 * 1024 + 1];
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ct);
        if (read == buffer.Length) throw new JsonException("Summary model response exceeds the limit.");
        using var envelope = JsonDocument.Parse(buffer.AsMemory(0, read));
        if (envelope.RootElement.ValueKind != JsonValueKind.Object
            || !envelope.RootElement.TryGetProperty("done", out var done) || done.ValueKind != JsonValueKind.True
            || !envelope.RootElement.TryGetProperty("response", out var text) || text.ValueKind != JsonValueKind.String)
            throw new JsonException("Incomplete summary model response.");
        using var result = JsonDocument.Parse(text.GetString()!);
        if (result.RootElement.ValueKind != JsonValueKind.Object
            || result.RootElement.EnumerateObject().Count() != 1
            || !result.RootElement.TryGetProperty("eventIndexes", out var indexes) || indexes.ValueKind != JsonValueKind.Array
            || indexes.GetArrayLength() is < 1 or > DeliverySummaryFacts.MaximumEvents)
            throw new JsonException("Invalid summary model selection.");
        var selected = new List<int>();
        foreach (var item in indexes.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var index)
                || index < 0 || index >= input.Events.Length || selected.Count > 0 && selected[^1] >= index)
                throw new JsonException("Invalid summary event index.");
            selected.Add(index);
        }
        return selected.ToArray();
    }
}
