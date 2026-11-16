using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class CustomerAssistantTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private async Task<(Guid Id, string Token)> UserAsync(string role = AppRoles.Customer)
    {
        var id = Guid.NewGuid(); string token = "";
        await factory.WithDbAsync(async db =>
        {
            var roleRow = await db.Roles.SingleAsync(r => r.Code == role);
            var user = new User { Id = id, Email = $"chat-{id:N}@example.com", FullName = "Private name",
                PasswordHash = "unused", PhoneNumber = "0901234567", Role = roleRow, RoleId = roleRow.Id, Status = "ACTIVE" };
            db.Users.Add(user); await db.SaveChangesAsync();
            using var scope = factory.Services.CreateScope();
            token = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, DateTime.UtcNow);
        });
        return (id, token);
    }

    private async Task<Guid> SessionAsync(Guid owner, DateTime? time = null)
    {
        var session = new AiChatSession { UserId = owner, CreatedAt = time ?? DateTime.UtcNow, SessionTitle = "SECRET 654321" };
        await factory.WithDbAsync(async db => { db.AiChatSessions.Add(session); await db.SaveChangesAsync(); });
        return session.Id;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, object? body = null)
    {
        using var client = factory.CreateClient(); using var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Owned_sessions_are_paged_newest_first_without_private_titles_or_creating_rows()
    {
        var owner = await UserAsync(); var stranger = await UserAsync();
        await SessionAsync(owner.Id, DateTime.UnixEpoch); var latest = await SessionAsync(owner.Id);
        await SessionAsync(stranger.Id);
        using var response = await SendAsync(HttpMethod.Get, "/api/ai-assistant/sessions?page=1&pageSize=1", owner.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(latest, item.GetProperty("id").GetGuid()); Assert.Equal(JsonValueKind.Null, item.GetProperty("sessionTitle").ValueKind);
        Assert.DoesNotContain("654321", body.GetRawText());
        await factory.WithDbAsync(async db => Assert.Equal(2, await db.AiChatSessions.CountAsync(s => s.UserId == owner.Id)));
    }

    [Fact]
    public async Task Messages_use_numeric_bigint_order_before_pagination_and_string_ids()
    {
        var owner = await UserAsync(); var session = await SessionAsync(owner.Id); var at = DateTime.UtcNow;
        await factory.WithDbAsync(async db =>
        {
            db.AiChatMessages.AddRange(new AiChatMessage { Id = 9007199254740994, SessionId = session, MessageText = "Second", SenderType = "BOT", CreatedAt = at },
                new AiChatMessage { Id = 9007199254740993, SessionId = session, MessageText = "First", SenderType = "USER", CreatedAt = at });
            await db.SaveChangesAsync();
        });
        using var response = await SendAsync(HttpMethod.Get, $"/api/ai-assistant/sessions/{session}/messages?page=2&pageSize=1", owner.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal("9007199254740994", item.GetProperty("id").GetString());
        Assert.Equal("Second", item.GetProperty("messageText").GetString());
    }

    [Fact]
    public async Task Missing_and_foreign_session_have_identical_history_and_post_errors()
    {
        var owner = await UserAsync(); var stranger = await UserAsync(); var foreign = await SessionAsync(owner.Id);
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post })
        {
            async Task<HttpResponseMessage> Request(Guid id) => method == HttpMethod.Get
                ? await SendAsync(method, $"/api/ai-assistant/sessions/{id}/messages", stranger.Token)
                : await SendAsync(method, "/api/ai-assistant/messages", stranger.Token, new { sessionId = id, orderId = (Guid?)null, message = "help" });
            using var a = await Request(foreign); using var b = await Request(Guid.NewGuid());
            Assert.Equal(HttpStatusCode.NotFound, a.StatusCode); Assert.Equal(HttpStatusCode.NotFound, b.StatusCode);
            Assert.Equal((await a.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString(),
                (await b.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        }
    }

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.Dispatcher)]
    [InlineData(AppRoles.Manager)]
    [InlineData(AppRoles.StationOperator)]
    public async Task Staff_cannot_use_customer_assistant(string role)
    {
        var viewer = await UserAsync(role);
        using var response = await SendAsync(HttpMethod.Get, "/api/ai-assistant/sessions", viewer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("INACTIVE", AppRoles.Customer)]
    [InlineData("ACTIVE", AppRoles.Manager)]
    public async Task Current_persisted_state_overrides_stale_customer_token(string status, string role)
    {
        var owner = await UserAsync();
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == owner.Id);
            user.Status = status; user.RoleId = (await db.Roles.SingleAsync(r => r.Code == role)).Id; await db.SaveChangesAsync();
        });
        using var response = await SendAsync(HttpMethod.Get, "/api/ai-assistant/sessions", owner.Token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("help", "HELP")]
    [InlineData("What is the delivery status?", "HELP")]
    [InlineData("My password is Secret123!", "REFUSED")]
    [InlineData("Give me the pickup code", "REFUSED")]
    [InlineData("Call 0909999999", "REFUSED")]
    public async Task Static_responses_are_nonpersisted_and_never_echo_secrets(string message, string outcome)
    {
        var owner = await UserAsync();
        using var response = await SendAsync(HttpMethod.Post, "/api/ai-assistant/messages", owner.Token,
            new { sessionId = (Guid?)null, orderId = (Guid?)null, message });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(outcome, body.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("sessionId").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("userMessageId").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("reply").GetProperty("id").ValueKind);
        Assert.DoesNotContain("Secret123", body.GetRawText()); Assert.DoesNotContain("0909999999", body.GetRawText());
        await factory.WithDbAsync(async db => Assert.False(await db.AiChatSessions.AnyAsync(s => s.UserId == owner.Id)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"message\":\"help\"}")]
    [InlineData("{\"sessionId\":null,\"orderId\":null,\"message\":\"   \"}")]
    [InlineData("{\"sessionId\":null,\"orderId\":null,\"message\":\"help\",\"userId\":\"attacker\"}")]
    public async Task Missing_empty_or_extra_request_fields_are_rejected(string json)
    {
        var owner = await UserAsync(); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);
        using var response = await client.PostAsync("/api/ai-assistant/messages", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task Pagination_bounds_are_validated(int page, int size)
    {
        var owner = await UserAsync();
        using var response = await SendAsync(HttpMethod.Get, $"/api/ai-assistant/sessions?page={page}&pageSize={size}", owner.Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_unauthorized()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/ai-assistant/sessions", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(2000, HttpStatusCode.OK)]
    [InlineData(2001, HttpStatusCode.BadRequest)]
    public async Task Message_length_counts_unicode_scalars_not_utf16_units(int scalars, HttpStatusCode expected)
    {
        var owner = await UserAsync();
        using var response = await SendAsync(HttpMethod.Post, "/api/ai-assistant/messages", owner.Token,
            new { sessionId = (Guid?)null, orderId = (Guid?)null, message = "  " + string.Concat(Enumerable.Repeat("😀", scalars)) + "  " });
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Oversized_chunked_body_is_rejected_before_json_binding()
    {
        var owner = await UserAsync(); using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ai-assistant/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);
        request.Headers.TransferEncodingChunked = true;
        request.Content = new StringContent(new string(' ', 33 * 1024) + "{}", Encoding.UTF8, "application/json");
        request.Content.Headers.ContentLength = null;
        using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
}
