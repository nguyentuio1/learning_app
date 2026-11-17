// LA-168: Customer AI assistant acceptance and regression tests
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class CustomerAssistantAcceptanceTests(AssistantGenerationFactory factory) : IClassFixture<AssistantGenerationFactory>
{
    private async Task<HttpResponseMessage> PostMessageAsync(
        string token, string message, Guid? sessionId = null, Guid? orderId = null)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ai-assistant/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { sessionId, orderId, message });
        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string path, string token)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Grounded_eta_and_history_answers_reflect_recorded_facts_without_fabricating_arrival_or_leaking_secrets()
    {
        var user = await AssistantTestData.SeedAsync(factory);

        // 1. ARRIVED order + Eta intent -> arrival countdown no longer available
        factory.Model.Handler = (_, _) => Task.FromResult(AssistantIntent.Eta);
        using (var arrivedEtaRes = await PostMessageAsync(user.Token, "When will my package arrive?", orderId: user.OrderId))
        {
            Assert.Equal(HttpStatusCode.OK, arrivedEtaRes.StatusCode);
            var body = await arrivedEtaRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("ANSWERED", body.GetProperty("outcome").GetString());
            var text = body.GetProperty("reply").GetProperty("messageText").GetString()!;
            Assert.Contains("arrived at the destination station and awaiting pickup", text);
            Assert.Contains("An arrival countdown is no longer available.", text);
            Assert.DoesNotContain("654321", body.GetRawText());
            Assert.DoesNotContain("0909999999", body.GetRawText());
        }

        // 2. IN_TRANSIT order with future EstimatedDeliveryTime -> reports stored arrival estimate
        var now = DateTime.UtcNow;
        var futureEta = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc).AddDays(3);
        await factory.WithDbAsync(async db =>
        {
            var order = await db.DeliveryOrders.SingleAsync(o => o.Id == user.OrderId);
            order.Status = "IN_TRANSIT";
            order.EstimatedDeliveryTime = futureEta;
            db.DeliveryStatusLogs.Add(new DeliveryStatusLog
            {
                OrderId = order.Id,
                FromStatus = "APPROVED",
                ToStatus = "IN_TRANSIT",
                Remarks = "Private operator note 654321",
                CreatedAt = now.AddHours(-1)
            });
            await db.SaveChangesAsync();
        });

        using (var inTransitEtaRes = await PostMessageAsync(user.Token, "What is the estimated arrival?", orderId: user.OrderId))
        {
            Assert.Equal(HttpStatusCode.OK, inTransitEtaRes.StatusCode);
            var body = await inTransitEtaRes.Content.ReadFromJsonAsync<JsonElement>();
            var text = body.GetProperty("reply").GetProperty("messageText").GetString()!;
            Assert.Contains("in transit", text);
            Assert.Contains(futureEta.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture), text);
        }

        // 2b. IN_TRANSIT order with overdue EstimatedDeliveryTime -> reports estimate passed without fabricating a new arrival
        await factory.WithDbAsync(async db =>
        {
            var order = await db.DeliveryOrders.SingleAsync(o => o.Id == user.OrderId);
            order.EstimatedDeliveryTime = now.AddMinutes(-10);
            await db.SaveChangesAsync();
        });

        using (var overdueEtaRes = await PostMessageAsync(user.Token, "What is the estimated arrival now?", orderId: user.OrderId))
        {
            Assert.Equal(HttpStatusCode.OK, overdueEtaRes.StatusCode);
            var body = await overdueEtaRes.Content.ReadFromJsonAsync<JsonElement>();
            var text = body.GetProperty("reply").GetProperty("messageText").GetString()!;
            Assert.Contains("The recorded arrival estimate has passed; a revised estimate is not available.", text);
        }

        // 3. History intent -> summarizes chronological events without exposing remarks or pickup codes
        factory.Model.Handler = (_, _) => Task.FromResult(AssistantIntent.History);
        using (var historyRes = await PostMessageAsync(user.Token, "Show recent delivery history", orderId: user.OrderId))
        {
            Assert.Equal(HttpStatusCode.OK, historyRes.StatusCode);
            var body = await historyRes.Content.ReadFromJsonAsync<JsonElement>();
            var text = body.GetProperty("reply").GetProperty("messageText").GetString()!;
            Assert.Contains("Recent recorded events:", text);
            Assert.DoesNotContain("654321", body.GetRawText());
            Assert.DoesNotContain("Private operator note", body.GetRawText());
        }
    }

    [Fact]
    public async Task Multi_turn_conversation_appends_messages_and_delete_endpoint_removes_session_and_messages_atomically()
    {
        var user = await AssistantTestData.SeedAsync(factory);
        factory.Model.Handler = (_, _) => Task.FromResult(AssistantIntent.Status);

        // Turn 1: creates a new owned session
        using var turn1Res = await PostMessageAsync(user.Token, "First question about status", sessionId: null, orderId: user.OrderId);
        Assert.Equal(HttpStatusCode.OK, turn1Res.StatusCode);
        var turn1Body = await turn1Res.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = turn1Body.GetProperty("sessionId").GetGuid();

        // Turn 2: appends to the existing owned session
        using var turn2Res = await PostMessageAsync(user.Token, "Second question about status", sessionId: sessionId, orderId: user.OrderId);
        Assert.Equal(HttpStatusCode.OK, turn2Res.StatusCode);
        var turn2Body = await turn2Res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(sessionId, turn2Body.GetProperty("sessionId").GetGuid());

        // Read messages and verify 4 messages in chronological bigint order
        using var listRes = await SendAuthorizedAsync(HttpMethod.Get, $"/api/ai-assistant/sessions/{sessionId}/messages?page=1&pageSize=20", user.Token);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var listBody = await listRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, listBody.GetProperty("totalCount").GetInt32());
        var items = listBody.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new[] { "USER", "BOT", "USER", "BOT" }, items.Select(i => i.GetProperty("senderType").GetString()));
        var ids = items.Select(i => long.Parse(i.GetProperty("id").GetString()!)).ToArray();
        Assert.Equal(ids.OrderBy(x => x).ToArray(), ids);

        // Delete owned session -> 204 No Content and removes session + messages
        using var delRes = await SendAuthorizedAsync(HttpMethod.Delete, $"/api/ai-assistant/sessions/{sessionId}", user.Token);
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        using var afterGet = await SendAuthorizedAsync(HttpMethod.Get, $"/api/ai-assistant/sessions/{sessionId}/messages", user.Token);
        Assert.Equal(HttpStatusCode.NotFound, afterGet.StatusCode);

        using var afterDel = await SendAuthorizedAsync(HttpMethod.Delete, $"/api/ai-assistant/sessions/{sessionId}", user.Token);
        Assert.Equal(HttpStatusCode.NotFound, afterDel.StatusCode);
    }

    [Fact]
    public async Task Missing_order_selection_returns_clarification_when_orders_exist_and_help_when_account_has_no_orders_without_persisting()
    {
        var customerWithOrder = await AssistantTestData.SeedAsync(factory);
        var called = false;
        factory.Model.Handler = (_, _) =>
        {
            called = true;
            return Task.FromResult(AssistantIntent.Status);
        };

        using (var clarifyRes = await PostMessageAsync(customerWithOrder.Token, "Where is my order?", sessionId: null, orderId: null))
        {
            Assert.Equal(HttpStatusCode.OK, clarifyRes.StatusCode);
            var body = await clarifyRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("CLARIFICATION_REQUIRED", body.GetProperty("outcome").GetString());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("sessionId").ValueKind);
            Assert.False(called);
        }

        // Create a customer with zero orders
        Guid emptyCustomerId = Guid.Empty;
        string emptyCustomerToken = "";
        await factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.SingleAsync(r => r.Code == AppRoles.Customer);
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"no-orders-{Guid.NewGuid():N}@example.com",
                FullName = "No Orders Customer",
                PasswordHash = "unused",
                PhoneNumber = $"07{Random.Shared.Next(10000000, 99999999)}",
                Status = "ACTIVE",
                Role = role,
                RoleId = role.Id
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            emptyCustomerId = user.Id;
            using var scope = factory.Services.CreateScope();
            emptyCustomerToken = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, DateTime.UtcNow);
        });

        using (var helpRes = await PostMessageAsync(emptyCustomerToken, "Where is my order?", sessionId: null, orderId: null))
        {
            Assert.Equal(HttpStatusCode.OK, helpRes.StatusCode);
            var body = await helpRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("HELP", body.GetProperty("outcome").GetString());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("sessionId").ValueKind);
            Assert.False(called);
        }

        await factory.WithDbAsync(async db =>
        {
            Assert.False(await db.AiChatSessions.AnyAsync(s => s.UserId == customerWithOrder.Id || s.UserId == emptyCustomerId));
        });
    }
}

public class CustomerAssistantDisabledDefaultTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task Disabled_assistant_rejects_model_generation_with_503_while_owned_session_reads_and_deletion_remain_available()
    {
        var user = await AssistantTestData.SeedAsync(factory);
        var sessionId = Guid.NewGuid();
        await factory.WithDbAsync(async db =>
        {
            db.AiChatSessions.Add(new AiChatSession { Id = sessionId, UserId = user.Id });
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();

        // Model generation POST with orderId returns 503 when CustomerAssistant:Enabled defaults to false
        using (var postReq = new HttpRequestMessage(HttpMethod.Post, "/api/ai-assistant/messages"))
        {
            postReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
            postReq.Content = JsonContent.Create(new { sessionId = (Guid?)sessionId, orderId = (Guid?)user.OrderId, message = "Status?" });
            using var postRes = await client.SendAsync(postReq);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, postRes.StatusCode);
        }

        // Owned session listing and deletion still succeed while generation is disabled
        using (var getReq = new HttpRequestMessage(HttpMethod.Get, "/api/ai-assistant/sessions"))
        {
            getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
            using var getRes = await client.SendAsync(getReq);
            Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        }

        using (var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/ai-assistant/sessions/{sessionId}"))
        {
            delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
            using var delRes = await client.SendAsync(delReq);
            Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);
        }
    }
}
