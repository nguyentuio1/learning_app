using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Api.Services;

public class LocalEtaModelGateway(HttpClient client, IOptions<EtaPredictionOptions> options) : IEtaModelGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public bool IsConfigured => options.Value.Enabled && options.Value.HasValidConfiguration();
    public string ModelVersion => options.Value.ModelRevision.Trim();

    public async Task<int> PredictDurationAsync(EtaModelInput input, CancellationToken ct)
    {
        if (!IsConfigured || !options.Value.TryGetEndpoint(out var endpoint))
            throw new HttpRequestException("ETA model is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new {
                model = options.Value.ModelName.Trim(), stream = false,
                system = "Estimate the full-flight duration between stations from the supplied numeric features. Return only JSON with integer durationMinutes from 1 to 1440. Weather is unknown. Do not invent weather, personal data, or a confidence score.",
                prompt = JsonSerializer.Serialize(input, JsonOptions),
                format = new { type = "object", required = new[] { "durationMinutes" }, additionalProperties = false,
                    properties = new { durationMinutes = new { type = "integer", minimum = 1, maximum = 1440 } } },
                options = new { temperature = 0, num_predict = 64 }
            })
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[16 * 1024 + 1];
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ct);
        if (read == buffer.Length) throw new JsonException("ETA model response exceeds the limit.");
        using var envelope = JsonDocument.Parse(buffer.AsMemory(0, read));
        if (envelope.RootElement.ValueKind != JsonValueKind.Object
            || !envelope.RootElement.TryGetProperty("done", out var done) || done.ValueKind != JsonValueKind.True
            || !envelope.RootElement.TryGetProperty("response", out var text) || text.ValueKind != JsonValueKind.String)
            throw new JsonException("Incomplete ETA model response.");
        using var result = JsonDocument.Parse(text.GetString()!);
        if (result.RootElement.ValueKind != JsonValueKind.Object
            || !result.RootElement.TryGetProperty("durationMinutes", out var duration)
            || duration.ValueKind != JsonValueKind.Number || !duration.TryGetInt32(out var minutes) || minutes is < 1 or > 1440)
            throw new JsonException("Invalid ETA model duration.");
        return minutes;
    }
}
