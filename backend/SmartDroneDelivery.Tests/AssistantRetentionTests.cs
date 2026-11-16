using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class AssistantRetentionTests(AssistantGenerationFactory factory) : IClassFixture<AssistantGenerationFactory>
{
    private async Task<Guid> SessionAsync(Guid owner, bool expired)
    {
        var id = Guid.NewGuid();
        await factory.WithDbAsync(async db =>
        {
            db.AiChatSessions.Add(new AiChatSession { Id = id, UserId = owner, CreatedAt = DateTime.UtcNow.AddDays(expired ? -31 : -1) });
            db.AiChatMessages.Add(new AiChatMessage { SessionId = id, SenderType = "USER", MessageText = "status?" });
            await db.SaveChangesAsync();
        });
        return id;
    }
    [Fact]
    public async Task Approved_policy_filters_expired_sessions_and_cleanup_removes_their_messages()
    {
        var user = await AssistantTestData.SeedAsync(factory); var expired = await SessionAsync(user.Id, true); var active = await SessionAsync(user.Id, false);
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        using var list = await client.GetAsync("/api/ai-assistant/sessions");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(active, Assert.Single(body.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        using var readExpired = await client.GetAsync($"/api/ai-assistant/sessions/{expired}/messages");
        Assert.Equal(HttpStatusCode.NotFound, readExpired.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<AssistantRetentionService>().CleanupBatchAsync(default) >= 1);
        await factory.WithDbAsync(async db =>
        {
            Assert.False(await db.AiChatSessions.AnyAsync(s => s.Id == expired)); Assert.False(await db.AiChatMessages.AnyAsync(m => m.SessionId == expired));
            Assert.True(await db.AiChatSessions.AnyAsync(s => s.Id == active));
        });
    }
    [Fact]
    public async Task Customer_delete_removes_owned_conversation_and_missing_foreign_are_identical()
    {
        var user = await AssistantTestData.SeedAsync(factory); var other = await AssistantTestData.SeedAsync(factory);
        var owned = await SessionAsync(user.Id, false); var foreign = await SessionAsync(other.Id, false);
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        using var deleted = await client.DeleteAsync($"/api/ai-assistant/sessions/{owned}"); Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var forbidden = await client.DeleteAsync($"/api/ai-assistant/sessions/{foreign}");
        using var missing = await client.DeleteAsync($"/api/ai-assistant/sessions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal((await forbidden.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString(),
            (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        await factory.WithDbAsync(async db =>
        {
            Assert.False(await db.AiChatMessages.AnyAsync(m => m.SessionId == owned)); Assert.True(await db.AiChatSessions.AnyAsync(s => s.Id == foreign));
        });
    }
    [Fact]
    public async Task Default_policy_neither_deletes_legacy_data_nor_enables_generation()
    {
        using var unapproved = new AuthApiFactory(); var user = await AssistantTestData.SeedAsync(unapproved); var session = Guid.NewGuid();
        await unapproved.WithDbAsync(async db =>
        {
            db.AiChatSessions.Add(new AiChatSession { Id = session, UserId = user.Id, CreatedAt = DateTime.UtcNow.AddDays(-90) }); await db.SaveChangesAsync();
        });
        using var scope = unapproved.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AssistantRetentionService>().CleanupBatchAsync(default));
        using var client = unapproved.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        using var response = await client.PostAsJsonAsync("/api/ai-assistant/messages", new { sessionId = session, orderId = user.OrderId, message = "status?" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await unapproved.WithDbAsync(async db => { Assert.True(await db.AiChatSessions.AnyAsync(s => s.Id == session)); Assert.Empty(await db.AiChatMessages.Where(m => m.SessionId == session).ToArrayAsync()); });
    }
}
