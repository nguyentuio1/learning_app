using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class LocalEtaModelTests
{
    [Fact]
    public async Task Configured_model_receives_only_minimized_features_and_structured_output_request()
    {
        var handler = new ModelHandler("{\"done\":true,\"response\":\"{\\\"durationMinutes\\\":18,\\\"confidenceScore\\\":0.99}\"}");
        using var client = new HttpClient(handler);
        var gateway = new LocalEtaModelGateway(client, Options.Create(Settings()));
        Assert.True(gateway.IsConfigured);
        Assert.Equal("sha256-test-revision", gateway.ModelVersion);
        Assert.Equal(18, await gateway.PredictDurationAsync(new(12.35m, 450m, false, 12, null), default));
        Assert.Equal("http://127.0.0.1:11434/api/generate", handler.Uri!.ToString());
        using var json = JsonDocument.Parse(handler.Request!);
        Assert.False(json.RootElement.GetProperty("stream").GetBoolean());
        using var features = JsonDocument.Parse(json.RootElement.GetProperty("prompt").GetString()!);
        Assert.Equal(5, features.RootElement.EnumerateObject().Count());
        Assert.Equal(450m, features.RootElement.GetProperty("weightGram").GetDecimal());
        Assert.Equal(JsonValueKind.Null, features.RootElement.GetProperty("weatherCondition").ValueKind);
        Assert.False(features.RootElement.TryGetProperty("orderId", out _));
        Assert.False(features.RootElement.TryGetProperty("recipientPhone", out _));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"done\":false,\"response\":\"{\\\"durationMinutes\\\":18}\"}")]
    [InlineData("{\"done\":true,\"response\":\"{\\\"durationMinutes\\\":0}\"}")]
    [InlineData("{\"done\":true,\"response\":\"{\\\"durationMinutes\\\":18.5}\"}")]
    [InlineData("{\"done\":true,\"response\":\"not json\"}")]
    public async Task Malformed_or_incomplete_model_output_is_rejected(string response)
    {
        using var client = new HttpClient(new ModelHandler(response));
        var gateway = new LocalEtaModelGateway(client, Options.Create(Settings()));
        await Assert.ThrowsAnyAsync<JsonException>(() => gateway.PredictDurationAsync(new(12, 450, false, 12, null), default));
    }

    [Fact]
    public async Task Default_or_external_configuration_makes_no_request()
    {
        var handler = new ModelHandler("{}");
        using var client = new HttpClient(handler);
        var gateway = new LocalEtaModelGateway(client, Options.Create(new EtaPredictionOptions()));
        Assert.False(gateway.IsConfigured);
        await Assert.ThrowsAsync<HttpRequestException>(() => gateway.PredictDurationAsync(new(12, 450, false, 12, null), default));
        var external = Settings(); external.BaseUrl = "https://example.com";
        Assert.False(new LocalEtaModelGateway(client, Options.Create(external)).IsConfigured);
        Assert.Null(handler.Request);
    }

    private static EtaPredictionOptions Settings() => new() { Enabled = true, ModelName = "local-test-model",
        ModelRevision = "sha256-test-revision" };

    private sealed class ModelHandler(string response) : HttpMessageHandler
    {
        public string? Request { get; private set; }
        public Uri? Uri { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Uri = request.RequestUri;
            Request = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
