using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Api.Services;

public class GeminiModelHandler(IOptions<ModelProviderOptions> options, bool assistant = false, TimeProvider? clock = null) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var provider = options.Value;
        if (provider.Provider == "Ollama") return await base.SendAsync(request, ct);
        if (!provider.HasValidConfiguration()) throw new HttpRequestException("AI provider is not configured.");
        if (assistant && provider.GeminiAssistantApprovedPolicyVersion != ModelProviderOptions.GeminiAssistantPolicyVersion)
            throw new HttpRequestException("Cloud assistant policy is not approved.");
        if (request.Method != HttpMethod.Post || request.RequestUri?.IsLoopback != true || request.Content == null)
            throw new HttpRequestException("Unsupported structured model request.");

        using var input = JsonDocument.Parse(await request.Content.ReadAsStringAsync(ct));
        var root = input.RootElement;
        var model = root.GetProperty("model").GetString();
        if (string.IsNullOrWhiteSpace(model) || model.Length > 200)
            throw new JsonException("A model identifier is required.");
        var settings = root.GetProperty("options");
        var endpoint = new Uri($"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                systemInstruction = new { parts = new[] { new { text = root.GetProperty("system").GetString() } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = root.GetProperty("prompt").GetString() } } } },
                generationConfig = new
                {
                    temperature = settings.GetProperty("temperature").GetDouble(),
                    maxOutputTokens = settings.GetProperty("num_predict").GetInt32(),
                    responseMimeType = "application/json", responseJsonSchema = root.GetProperty("format").Clone()
                }
            });
        if (!provider.GeminiRetryTransientFailures)
            return await GenerateAsync(endpoint, payload, provider.GeminiApiKey, ct);

        // Inference is read-only. Retry at most once, before any application persistence.
        // Two four-second attempts + backoff fit inside the services' existing ten-second deadline.
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4), clock ?? TimeProvider.System);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            try
            {
                var response = await GenerateAsync(endpoint, payload, provider.GeminiApiKey, linked.Token);
                if (attempt != 0 || response.Headers.RetryAfter != null ||
                    response.StatusCode is not (HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout))
                    return response;
                response.Dispose();
            }
            catch (OperationCanceledException) when (attempt == 0 && timeout.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                // Cancel the stalled inference attempt; never replay application/API writes.
            }
            await Task.Delay(TimeSpan.FromMilliseconds(200), clock ?? TimeProvider.System, ct);
        }
    }

    private async Task<HttpResponseMessage> GenerateAsync(Uri endpoint, byte[] payload, string key, CancellationToken ct)
    {
        using var outbound = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            // Fresh message/content on each attempt, with a known Content-Length.
            Content = new ByteArrayContent(payload)
        };
        outbound.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        outbound.Headers.Add("x-goog-api-key", key);
        var upstream = await base.SendAsync(outbound, ct);
        if (!upstream.IsSuccessStatusCode) return upstream; // Preserve quota/auth failures; never invent a successful response.
        using (upstream)
        {
            await using var stream = await upstream.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[64 * 1024 + 1];
            var count = await stream.ReadAtLeastAsync(buffer, buffer.Length, false, ct);
            if (count == buffer.Length) throw new JsonException("Gemini response exceeds the limit.");
            using var result = JsonDocument.Parse(buffer.AsMemory(0, count));
            if (result.RootElement.ValueKind != JsonValueKind.Object
                || !result.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array
                || candidates.GetArrayLength() != 1 || candidates[0].ValueKind != JsonValueKind.Object
                || !candidates[0].TryGetProperty("finishReason", out var reason) || reason.ValueKind != JsonValueKind.String
                || reason.GetString() != "STOP" || !candidates[0].TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object
                || !content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
                throw new JsonException("Incomplete or blocked Gemini response.");
            if (parts.EnumerateArray().Any(part => part.ValueKind != JsonValueKind.Object))
                throw new JsonException("Malformed Gemini response part.");
            var texts = parts.EnumerateArray().Where(part => !part.TryGetProperty("thought", out var thought) || thought.ValueKind != JsonValueKind.True)
                .Where(part => part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                .Select(part => part.GetProperty("text").GetString());
            var generated = string.Concat(texts);
            if (string.IsNullOrWhiteSpace(generated)) throw new JsonException("Gemini response has no structured text.");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { done = true, response = generated })
            };
        }
    }
}
