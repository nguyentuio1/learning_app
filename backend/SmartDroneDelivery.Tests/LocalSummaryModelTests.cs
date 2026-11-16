using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class LocalSummaryModelTests
{
    private static readonly SummaryModelInput Facts = new("ARRIVED", [new(null, "PENDING", DateTime.UnixEpoch),
        new("IN_TRANSIT", "ARRIVED", DateTime.UnixEpoch.AddHours(1))]);
    private static DeliverySummaryOptions Settings() => new() { Enabled = true, ModelName = "test-local", ModelRevision = "test-revision" };

    [Fact]
    public async Task Local_structured_output_selects_facts_without_passing_ids_or_remarks()
    {
        var handler = new Handler(); var model = new LocalSummaryModelGateway(new HttpClient(handler), Options.Create(Settings()));
        Assert.True(model.IsConfigured);
        Assert.Equal(new[] { 0, 1 }, await model.SelectEventsAsync(Facts, default));
        Assert.Equal("http://127.0.0.1:11434/api/generate", handler.Url);
        using var request = JsonDocument.Parse(handler.Body!);
        Assert.Equal("test-local", request.RootElement.GetProperty("model").GetString());
        Assert.False(request.RootElement.GetProperty("stream").GetBoolean());
        Assert.DoesNotContain("orderId", request.RootElement.GetProperty("prompt").GetString());
        Assert.Contains("ARRIVED", request.RootElement.GetProperty("prompt").GetString());
    }

    [Theory]
    [InlineData("https://external.example")]
    [InlineData("file:///tmp/model")]
    [InlineData("http://user:pass@localhost:11434")]
    [InlineData("http://localhost:11434?secret=value")]
    [InlineData("http://localhost:11434#fragment")]
    public async Task Nonlocal_or_credential_bearing_endpoint_is_never_called(string url)
    {
        var settings = Settings(); settings.BaseUrl = url;
        var handler = new Handler(); var model = new LocalSummaryModelGateway(new HttpClient(handler), Options.Create(settings));
        Assert.False(model.IsConfigured);
        await Assert.ThrowsAsync<HttpRequestException>(() => model.SelectEventsAsync(Facts, default));
        Assert.Null(handler.Url);
    }

    [Theory]
    [InlineData("{\"done\":false,\"response\":\"{}\"}")]
    [InlineData("{\"done\":true,\"response\":\"{\\\"eventIndexes\\\":[-1]}\"}")]
    [InlineData("{\"done\":true,\"response\":\"{\\\"eventIndexes\\\":[0,1],\\\"text\\\":\\\"<script>fake delivery</script>\\\"}\"}")]
    [InlineData("{\"done\":true,\"response\":\"{\\\"eventIndexes\\\":[0.5]}\"}")]
    [InlineData("{\"done\":true,\"response\":\"{\\\"eventIndexes\\\":[0,0]}\"}")]
    public async Task Invalid_unfinished_or_model_authored_prose_is_rejected(string body)
    {
        var handler = new Handler { Response = body };
        var model = new LocalSummaryModelGateway(new HttpClient(handler), Options.Create(Settings()));
        await Assert.ThrowsAsync<JsonException>(() => model.SelectEventsAsync(Facts, default));
    }

    [Fact]
    public async Task Excessive_response_is_rejected_before_parsing()
    {
        var model = new LocalSummaryModelGateway(new HttpClient(new Handler { Response = new string('x', 16 * 1024 + 1) }), Options.Create(Settings()));
        await Assert.ThrowsAsync<JsonException>(() => model.SelectEventsAsync(Facts, default));
    }

    private class Handler : HttpMessageHandler
    {
        public string? Url; public string? Body;
        public string Response = "{\"done\":true,\"response\":\"{\\\"eventIndexes\\\":[0,1]}\"}";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri!.ToString(); Body = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent(Response, Encoding.UTF8, "application/json") };
        }
    }
}
