using System.Diagnostics;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;
using Xunit.Abstractions;

namespace SmartDroneDelivery.Tests;

/// <summary>Opt-in real provider checks with synthetic facts only; never approves application policy.</summary>
public class LiveGeminiGatewayTests(ITestOutputHelper output)
{
    private static string Model => Environment.GetEnvironmentVariable("TEST_GEMINI_MODEL") ?? "gemini-3.5-flash-lite";

    [LiveGeminiFact]
    public async Task Synthetic_eta_uses_the_real_provider_and_bounded_duration_contract()
    {
        using var client = Client();
        var gateway = new LocalEtaModelGateway(client, Options.Create(new EtaPredictionOptions
        { Enabled = true, ModelName = Model, ModelRevision = Model }));
        var timer = Stopwatch.StartNew();
        var minutes = await gateway.PredictDurationAsync(new EtaModelInput(11.3m, 500, false, 60, null), CancellationToken.None);
        Assert.InRange(minutes, 1, 1440);
        output.WriteLine($"model={Model}; ETA durationMinutes={minutes}; elapsedMs={timer.ElapsedMilliseconds}; synthetic transport validation only, not prediction-quality approval");
    }

    [LiveGeminiFact]
    public async Task Synthetic_summary_selects_only_known_events_and_renders_grounded_facts()
    {
        using var client = Client();
        var gateway = new LocalSummaryModelGateway(client, Options.Create(new DeliverySummaryOptions
        { Enabled = true, ModelName = Model, ModelRevision = Model }));
        var start = new DateTime(2026, 11, 18, 1, 0, 0, DateTimeKind.Utc);
        var input = new SummaryModelInput("DELIVERED", [new(null, "PENDING", start),
            new("IN_TRANSIT", "ARRIVED", start.AddMinutes(20)), new("ARRIVED", "DELIVERED", start.AddMinutes(25))]);
        var timer = Stopwatch.StartNew();
        var selected = await gateway.SelectEventsAsync(input, CancellationToken.None);
        var text = GroundedSummaryRenderer.Render(input, selected);
        Assert.Contains("receipt confirmed", text);
        Assert.Contains("awaiting pickup", text);
        output.WriteLine($"model={Model}; eventIndexes=[{string.Join(',', selected)}]; elapsedMs={timer.ElapsedMilliseconds}; summary={text}");
    }

    [LiveGeminiFact]
    public async Task Synthetic_assistant_classifies_without_order_details_or_persisting_a_conversation()
    {
        // Explicit test-only markers permit a synthetic gateway call. They are not a deployment approval.
        using var client = Client(assistant: true);
        var gateway = new LocalAssistantModelGateway(client, Options.Create(new CustomerAssistantOptions
        { Enabled = true, ModelName = Model, ModelRevision = Model,
            ApprovedPolicyVersion = CustomerAssistantOptions.PolicyVersion, BackupRetentionDays = 7 }));
        var timer = Stopwatch.StartNew();
        var intent = await gateway.ClassifyAsync(new AssistantModelInput("What is the current delivery status?", []), CancellationToken.None);
        Assert.Equal(AssistantIntent.Status, intent);
        output.WriteLine($"model={Model}; assistantIntent={intent}; elapsedMs={timer.ElapsedMilliseconds}; no customer data or persistence");
    }

    private HttpClient Client(bool assistant = false) => new(new GeminiModelHandler(Options.Create(new ModelProviderOptions
    {
        Provider = "Gemini", GeminiApiKey = Environment.GetEnvironmentVariable("TEST_GEMINI_API_KEY")!,
        GeminiAssistantApprovedPolicyVersion = ModelProviderOptions.GeminiAssistantPolicyVersion
    }), assistant) { InnerHandler = new LiveGeminiProvenanceHandler(output) }) { Timeout = TimeSpan.FromSeconds(20) };
}

internal sealed class LiveGeminiProvenanceHandler(ITestOutputHelper output) : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false })
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        var response = await base.SendAsync(request, ct);
        output.WriteLine($"providerStatus={(int)response.StatusCode}; elapsedMs={timer.ElapsedMilliseconds}");
        if (response.IsSuccessStatusCode)
        {
            using var parsed = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(ct));
            if (parsed.RootElement.TryGetProperty("modelVersion", out var version)) output.WriteLine($"returnedModelVersion={version.GetString()}");
            if (parsed.RootElement.TryGetProperty("candidates", out var candidates))
                foreach (var candidate in candidates.EnumerateArray())
                    if (candidate.TryGetProperty("finishReason", out var finish)) output.WriteLine($"finishReason={finish.GetString()}");
        }
        return response;
    }
}

public sealed class LiveGeminiFactAttribute : FactAttribute
{
    public LiveGeminiFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_GEMINI_API_KEY")))
            Skip = "Set TEST_GEMINI_API_KEY to run real Gemini gateway checks with synthetic data.";
    }
}
