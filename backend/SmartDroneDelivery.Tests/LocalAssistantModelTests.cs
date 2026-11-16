using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class LocalAssistantModelTests
{
    private static CustomerAssistantOptions Settings()
    {
        var options = new CustomerAssistantOptions(); AssistantTestData.EnableTestPolicy(options); return options;
    }
    [Fact]
    public async Task Configured_local_classifier_returns_only_an_allowlisted_intent()
    {
        var handler = new Handler(); var model = new LocalAssistantModelGateway(new HttpClient(handler), Options.Create(Settings()));
        Assert.True(model.IsConfigured);
        Assert.Equal(AssistantIntent.Status, await model.ClassifyAsync(new("Where is my delivery?", []), default));
        Assert.Equal("http://127.0.0.1:11434/api/generate", handler.Url);
        using var request = JsonDocument.Parse(handler.Body!); Assert.False(request.RootElement.GetProperty("stream").GetBoolean());
        var prompt = request.RootElement.GetProperty("prompt").GetString()!;
        Assert.DoesNotContain("orderId", prompt); Assert.DoesNotContain("userId", prompt);
        Assert.Contains("Where is my delivery?", prompt);
    }
    [Theory]
    [InlineData("https://external.example")]
    [InlineData("file:///tmp/model")]
    [InlineData("http://user:pass@localhost")]
    [InlineData("http://localhost?secret=x")]
    [InlineData("http://localhost#secret")]
    public async Task Unapproved_endpoint_is_never_contacted(string url)
    {
        var options = Settings(); options.BaseUrl = url; var handler = new Handler();
        var model = new LocalAssistantModelGateway(new HttpClient(handler), Options.Create(options));
        Assert.False(model.IsConfigured); await Assert.ThrowsAsync<HttpRequestException>(() => model.ClassifyAsync(new("status?", []), default));
        Assert.Null(handler.Url);
    }
    [Theory]
    [InlineData("{\"done\":false,\"response\":\"{}\"}")]
    [InlineData("{\"done\":true,\"response\":\"{\\\"intent\\\":\\\"DELETE_ALL\\\"}\"}")]
    [InlineData("{\"done\":true,\"response\":\"{\\\"intent\\\":1}\"}")]
    [InlineData("{\"done\":true,\"response\":\"{\\\"intent\\\":\\\"STATUS\\\",\\\"text\\\":\\\"Fake delivery completed\\\"}\"}")]
    public async Task Invalid_or_free_form_model_output_is_rejected(string body)
    {
        var model = new LocalAssistantModelGateway(new HttpClient(new Handler { Response = body }), Options.Create(Settings()));
        await Assert.ThrowsAsync<JsonException>(() => model.ClassifyAsync(new("status?", []), default));
    }
    [Fact]
    public async Task Oversized_model_output_is_rejected()
    {
        var model = new LocalAssistantModelGateway(new HttpClient(new Handler { Response = new string('a', 16385) }), Options.Create(Settings()));
        await Assert.ThrowsAsync<JsonException>(() => model.ClassifyAsync(new("status?", []), default));
    }
    [Fact]
    public void Persistence_requires_explicit_matching_policy_and_backup_configuration()
    {
        var options = new CustomerAssistantOptions(); Assert.False(options.HasApprovedRetention); Assert.False(options.Enabled);
        options.Enabled = true; Assert.False(options.HasValidConfiguration());
        options.ApprovedPolicyVersion = CustomerAssistantOptions.PolicyVersion; Assert.False(options.HasApprovedRetention);
        options.BackupRetentionDays = 30; Assert.True(options.HasApprovedRetention);
        options.ModelName = "test"; options.ModelRevision = "test-revision"; Assert.True(options.HasValidConfiguration());
        options.BackupRetentionDays = 31; Assert.False(options.HasValidConfiguration());
    }
    private class Handler : HttpMessageHandler
    {
        public string? Url; public string? Body;
        public string Response = "{\"done\":true,\"response\":\"{\\\"intent\\\":\\\"STATUS\\\"}\"}";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri!.ToString(); Body = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent(Response, Encoding.UTF8, "application/json") };
        }
    }
}
