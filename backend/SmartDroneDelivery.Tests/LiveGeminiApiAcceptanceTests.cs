using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;
using Xunit.Abstractions;

namespace SmartDroneDelivery.Tests;

/// <summary>Real provider + real API/service, synthetic data and an isolated in-memory database only.</summary>
public class LiveGeminiApiAcceptanceTests(ITestOutputHelper output)
{
    [LiveGeminiFact]
    public async Task Synthetic_three_service_flow_keeps_foreign_orders_isolated_and_records_real_provider_provenance()
    {
        using var app = new SyntheticGeminiApiFactory(output);
        var owner = await AssistantTestData.SeedAsync(app);
        var stranger = await AssistantTestData.SeedAsync(app);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);

        // Authorization failures must not consume provider quota.
        using (var denied = await client.PostAsJsonAsync($"/api/delivery-orders/{stranger.OrderId}/ai-summary", new { }))
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var denied = await client.PostAsJsonAsync("/api/ai-assistant/messages",
            new { sessionId = (Guid?)null, orderId = stranger.OrderId, message = "What is the delivery status?" }))
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(0, app.ProviderCalls);

        var timer = Stopwatch.StartNew();
        using (var summary = await client.PostAsJsonAsync($"/api/delivery-orders/{owner.OrderId}/ai-summary", new { }))
        {
            Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
            var body = await summary.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("GENERATED", body.GetProperty("outcome").GetString());
            Assert.DoesNotContain("654321", body.GetRawText());
            Assert.Contains("awaiting pickup", body.GetProperty("text").GetString());
            output.WriteLine($"summaryApi elapsedMs={timer.ElapsedMilliseconds}; outcome=GENERATED");
        }

        timer.Restart();
        using (var assistant = await client.PostAsJsonAsync("/api/ai-assistant/messages",
            new { sessionId = (Guid?)null, orderId = owner.OrderId, message = "What is the current delivery status?" }))
        {
            Assert.Equal(HttpStatusCode.OK, assistant.StatusCode);
            var body = await assistant.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("ANSWERED", body.GetProperty("outcome").GetString());
            Assert.DoesNotContain("654321", body.GetRawText());
            var session = body.GetProperty("sessionId").GetGuid();
            using var deleted = await client.DeleteAsync($"/api/ai-assistant/sessions/{session}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            output.WriteLine($"assistantApi elapsedMs={timer.ElapsedMilliseconds}; outcome=ANSWERED; synthetic session deleted");
        }

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await EtaPipelineData.SeedAsync(db);
        timer.Restart();
        var result = await scope.ServiceProvider.GetRequiredService<EtaPredictionService>().RecalculateAsync(order);
        Assert.Equal(EtaPredictionOutcome.Generated, result);
        var prediction = await db.AiEtaPredictions.SingleAsync(p => p.OrderId == order);
        Assert.InRange(prediction.PredictedDurationMinutes, 1, 1440);
        Assert.Null(prediction.ConfidenceScore);
        output.WriteLine($"etaPipeline elapsedMs={timer.ElapsedMilliseconds}; outcome={result}; durationMinutes={prediction.PredictedDurationMinutes}");
        Assert.Equal(3, app.ProviderCalls);
        output.WriteLine("Synthetic integration only; single samples do not prove prediction quality, p95 latency, concurrency, production persistence or cloud approval.");
    }
}

internal sealed class SyntheticGeminiApiFactory(ITestOutputHelper output) : AuthApiFactory
{
    private int providerCalls;
    public int ProviderCalls => providerCalls;
    private static string Model => Environment.GetEnvironmentVariable("TEST_GEMINI_MODEL") ?? "gemini-3.5-flash-lite";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<EtaPredictionOptions>(o => { o.Enabled = true; o.ModelName = Model; o.ModelRevision = Model; });
            services.PostConfigure<DeliverySummaryOptions>(o => { o.Enabled = true; o.ModelName = Model; o.ModelRevision = Model; });
            services.PostConfigure<CustomerAssistantOptions>(o =>
            {
                AssistantTestData.EnableTestPolicy(o); o.ModelName = Model; o.ModelRevision = Model;
            });
            services.RemoveAll<IEtaModelGateway>();
            services.RemoveAll<ISummaryModelGateway>();
            services.RemoveAll<IAssistantModelGateway>();
            services.AddSingleton<IEtaModelGateway>(sp => new LocalEtaModelGateway(Client(), sp.GetRequiredService<IOptions<EtaPredictionOptions>>()));
            services.AddSingleton<ISummaryModelGateway>(sp => new LocalSummaryModelGateway(Client(), sp.GetRequiredService<IOptions<DeliverySummaryOptions>>()));
            services.AddSingleton<IAssistantModelGateway>(sp => new LocalAssistantModelGateway(Client(true), sp.GetRequiredService<IOptions<CustomerAssistantOptions>>()));
        });
    }

    private HttpClient Client(bool assistant = false) => new(new GeminiModelHandler(Options.Create(new ModelProviderOptions
    {
        Provider = "Gemini", GeminiApiKey = Environment.GetEnvironmentVariable("TEST_GEMINI_API_KEY")!,
        // Test-only bypass is confined to this synthetic isolated fixture.
        GeminiAssistantApprovedPolicyVersion = ModelProviderOptions.GeminiAssistantPolicyVersion
    }), assistant) { InnerHandler = new ProvenanceHandler(this, output) }) { Timeout = TimeSpan.FromSeconds(20) };

    private sealed class ProvenanceHandler(SyntheticGeminiApiFactory app, ITestOutputHelper output) : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false })
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref app.providerCalls);
            var timer = Stopwatch.StartNew();
            var response = await base.SendAsync(request, ct);
            // Never record headers, prompts, keys or complete provider responses.
            var version = "unavailable";
            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                using var parsed = JsonDocument.Parse(bytes);
                if (parsed.RootElement.TryGetProperty("modelVersion", out var modelVersion)) version = modelVersion.GetString();
                foreach (var candidate in parsed.RootElement.GetProperty("candidates").EnumerateArray())
                    if (candidate.TryGetProperty("finishReason", out var finish)) output.WriteLine($"Gemini finishReason={finish.GetString()}");
            }
            output.WriteLine($"Gemini requestedModel={Model}; returnedModelVersion={version}; httpStatus={(int)response.StatusCode}; elapsedMs={timer.ElapsedMilliseconds}");
            return response;
        }
    }
}
