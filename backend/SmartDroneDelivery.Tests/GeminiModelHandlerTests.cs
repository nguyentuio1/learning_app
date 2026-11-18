using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class GeminiModelHandlerTests
{
    private static object Generation => new { model = "gemini-3.5-flash-lite", stream = false,
        system = "Classify a synthetic question.", prompt = "Where is the demo parcel?",
        format = new { type = "object", properties = new { intent = new { type = "string" } }, required = new[] { "intent" } },
        options = new { temperature = 0, num_predict = 32 } };

    [Fact]
    public async Task Explicit_Gemini_provider_sends_key_only_in_header_and_preserves_structured_gateway_contract()
    {
        using var client = Client("Gemini", async request =>
        {
            Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent", request.RequestUri!.AbsoluteUri);
            Assert.Equal("synthetic-test-key", request.Headers.GetValues("x-goog-api-key").Single());
            Assert.Null(request.Headers.Authorization);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("Classify a synthetic question.", body.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
            Assert.Equal("Where is the demo parcel?", body.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());
            var config = body.RootElement.GetProperty("generationConfig");
            Assert.Equal(32, config.GetProperty("maxOutputTokens").GetInt32());
            Assert.Equal("application/json", config.GetProperty("responseMimeType").GetString());
            Assert.True(config.TryGetProperty("responseJsonSchema", out _));
            return Json(HttpStatusCode.OK, """{"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":"{\"intent\":\"STATUS\"}"}]}}],"modelVersion":"gemini-3.5-flash-lite"}""");
        });
        using var response = await client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(envelope.GetProperty("done").GetBoolean());
        Assert.Equal("{\"intent\":\"STATUS\"}", envelope.GetProperty("response").GetString());
    }

    [Fact]
    public async Task Default_Ollama_provider_keeps_requests_local_without_a_Gemini_key()
    {
        using var client = Client("Ollama", request =>
        {
            Assert.Equal("http://127.0.0.1:11434/api/generate", request.RequestUri!.AbsoluteUri);
            Assert.False(request.Headers.Contains("x-goog-api-key"));
            return Task.FromResult(Json(HttpStatusCode.OK, """{"done":true,"response":"{}"}"""));
        });
        using var response = await client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Quota_error_is_returned_without_retrying_or_falling_back_to_invented_output()
    {
        var calls = 0;
        using var client = Client("Gemini", _ => { calls++; return Task.FromResult(Json(HttpStatusCode.TooManyRequests, "{}")); });
        using var response = await client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("""{"candidates":[]}""")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"candidates":[null]}""")]
    [InlineData("""{"candidates":[{"finishReason":7,"content":{"parts":[]}}]}""")]
    [InlineData("""{"candidates":[{"finishReason":"STOP","content":{"parts":[null]}}]}""")]
    [InlineData("""{"candidates":[{"finishReason":"MAX_TOKENS","content":{"parts":[{"text":"{}"}]}}]}""")]
    public async Task Incomplete_or_blocked_output_cannot_be_accepted_as_complete_generation(string body)
    {
        using var client = Client("Gemini", _ => Task.FromResult(Json(HttpStatusCode.OK, body)));
        await Assert.ThrowsAsync<JsonException>(() => client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation));
    }

    [Fact]
    public async Task Assistant_text_is_not_sent_to_cloud_under_the_original_local_only_policy()
    {
        var calls = 0;
        using var client = new HttpClient(new GeminiModelHandler(Options.Create(new ModelProviderOptions
        { Provider = "Gemini", GeminiApiKey = "synthetic-test-key" }), assistant: true)
        { InnerHandler = new Probe(_ => { calls++; return Task.FromResult(Json(HttpStatusCode.OK, "{}")); }) });
        await Assert.ThrowsAsync<HttpRequestException>(() => client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", Generation));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Provider_configuration_is_explicit_and_requires_server_credentials_for_Gemini()
    {
        Assert.True(new ModelProviderOptions().HasValidConfiguration());
        Assert.False(new ModelProviderOptions { Provider = "Gemini" }.HasValidConfiguration());
        Assert.False(new ModelProviderOptions { Provider = "Unknown" }.HasValidConfiguration());
    }

    private static HttpClient Client(string provider, Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
        => new(new GeminiModelHandler(Options.Create(new ModelProviderOptions { Provider = provider, GeminiApiKey = "synthetic-test-key" }))
        { InnerHandler = new Probe(send) });
    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status) { Content = new StringContent(json) };
    private sealed class Probe(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
}
