using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class GeminiRecoveryApiTests
{
    [Fact]
    public async Task Recovered_inference_commits_exactly_one_owned_exchange_and_never_sends_order_secrets()
    {
        using var app = new RecoveryFactory(recover: true);
        var owner = await AssistantTestData.SeedAsync(app);
        var foreign = await AssistantTestData.SeedAsync(app);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);
        using (var denied = await client.PostAsJsonAsync("/api/ai-assistant/messages",
            new { sessionId = (Guid?)null, orderId = foreign.OrderId, message = "What is the delivery status?" }))
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(0, app.Calls);
        using var response = await client.PostAsJsonAsync("/api/ai-assistant/messages",
            new { sessionId = (Guid?)null, orderId = owner.OrderId, message = "What is the delivery status?" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, app.Calls);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ANSWERED", body.GetProperty("outcome").GetString());
        Assert.DoesNotContain("654321", body.GetRawText());
        await app.WithDbAsync(async db =>
        {
            var session = await db.AiChatSessions.SingleAsync();
            Assert.Equal(owner.Id, session.UserId);
            Assert.Equal(2, await db.AiChatMessages.CountAsync());
        });
    }

    [Fact]
    public async Task Exhausted_recovery_keeps_the_service_unavailable_without_persisting_a_partial_conversation()
    {
        using var app = new RecoveryFactory(recover: false);
        var owner = await AssistantTestData.SeedAsync(app);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);
        using var response = await client.PostAsJsonAsync("/api/ai-assistant/messages",
            new { sessionId = (Guid?)null, orderId = owner.OrderId, message = "What is the delivery status?" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(2, app.Calls);
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(0, await db.AiChatSessions.CountAsync());
            Assert.Equal(0, await db.AiChatMessages.CountAsync());
        });
    }

    private sealed class RecoveryFactory(bool recover) : AuthApiFactory
    {
        public int Calls { get; private set; }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<CustomerAssistantOptions>(AssistantTestData.EnableTestPolicy);
                services.RemoveAll<IAssistantModelGateway>();
                services.AddSingleton<IAssistantModelGateway>(sp => new LocalAssistantModelGateway(
                    new HttpClient(new GeminiModelHandler(Options.Create(new ModelProviderOptions
                    {
                        Provider = "Gemini", GeminiApiKey = "synthetic-test-key", GeminiRetryTransientFailures = true,
                        GeminiAssistantApprovedPolicyVersion = ModelProviderOptions.GeminiAssistantPolicyVersion
                    }), assistant: true) { InnerHandler = new Provider(this, recover) }),
                    sp.GetRequiredService<IOptions<CustomerAssistantOptions>>()));
            });
        }
        private sealed class Provider(RecoveryFactory app, bool recover) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var body = await request.Content!.ReadAsStringAsync(ct);
                Assert.DoesNotContain("654321", body);
                Assert.DoesNotContain("0909999999", body);
                return ++app.Calls == 1 || !recover
                    ? new(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") }
                    : new(HttpStatusCode.OK) { Content = new StringContent("""{"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":"{\"intent\":\"STATUS\"}"}]}}]}""") };
            }
        }
    }
}
