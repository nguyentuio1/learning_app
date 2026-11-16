using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class AssistantGenerationTests(AssistantGenerationFactory factory) : IClassFixture<AssistantGenerationFactory>
{
    private async Task<HttpResponseMessage> SendAsync(AssistantTestAccount user, string message = "What is the delivery status?", Guid? session = null, Guid? order = null)
    {
        using var client = factory.CreateClient(); using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ai-assistant/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        request.Content = JsonContent.Create(new { sessionId = session, orderId = order ?? user.OrderId, message });
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Grounded_status_answer_commits_owned_session_and_pair_without_contact_or_pickup_data()
    {
        var user = await AssistantTestData.SeedAsync(factory);
        factory.Model.Handler = (_, _) => Task.FromResult(AssistantIntent.Status);
        using var response = await SendAsync(user);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ANSWERED", body.GetProperty("outcome").GetString());
        var session = body.GetProperty("sessionId").GetGuid();
        Assert.Equal(JsonValueKind.String, body.GetProperty("userMessageId").ValueKind);
        var reply = body.GetProperty("reply"); Assert.Equal("BOT", reply.GetProperty("senderType").GetString());
        Assert.Contains("awaiting pickup", reply.GetProperty("messageText").GetString());
        Assert.DoesNotContain("receipt confirmed", reply.GetProperty("messageText").GetString());
        Assert.DoesNotContain("654321", body.GetRawText()); Assert.DoesNotContain("0909999999", body.GetRawText());
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.AiChatSessions.SingleAsync(s => s.Id == session);
            Assert.Equal(user.Id, stored.UserId); Assert.Null(stored.SessionTitle);
            var messages = await db.AiChatMessages.Where(m => m.SessionId == session).OrderBy(m => m.Id).ToArrayAsync();
            Assert.Equal(new[] { "USER", "BOT" }, messages.Select(m => m.SenderType));
            Assert.Equal(body.GetProperty("userMessageId").GetString(), messages[0].Id.ToString());
            Assert.Equal(reply.GetProperty("id").GetString(), messages[1].Id.ToString());
            Assert.Equal("ARRIVED", (await db.DeliveryOrders.SingleAsync(o => o.Id == user.OrderId)).Status);
        });
    }

    [Theory]
    [InlineData(AssistantIntent.Help, "HELP")]
    [InlineData(AssistantIntent.Clarify, "CLARIFICATION_REQUIRED")]
    [InlineData(AssistantIntent.Refuse, "REFUSED")]
    public async Task Nonanswers_never_mutate_existing_session(AssistantIntent intent, string outcome)
    {
        var user = await AssistantTestData.SeedAsync(factory); var session = Guid.NewGuid();
        await factory.WithDbAsync(async db => { db.AiChatSessions.Add(new AiChatSession { Id = session, UserId = user.Id }); await db.SaveChangesAsync(); });
        factory.Model.Handler = (_, _) => Task.FromResult(intent);
        using var response = await SendAsync(user, session: session);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(outcome, body.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("sessionId").ValueKind);
        await factory.WithDbAsync(async db => Assert.Empty(await db.AiChatMessages.Where(m => m.SessionId == session).ToArrayAsync()));
    }

    [Theory]
    [InlineData("source", HttpStatusCode.Conflict)]
    [InlineData("role", HttpStatusCode.Forbidden)]
    [InlineData("owner", HttpStatusCode.NotFound)]
    public async Task Changed_source_or_access_discards_candidate_and_creates_nothing(string change, HttpStatusCode expected)
    {
        var user = await AssistantTestData.SeedAsync(factory);
        factory.Model.Handler = async (_, _) =>
        {
            await factory.WithDbAsync(async db =>
            {
                if (change == "source") (await db.DeliveryStatusLogs.SingleAsync(l => l.OrderId == user.OrderId)).ToStatus = "FAILED";
                else if (change == "role") (await db.Users.SingleAsync(u => u.Id == user.Id)).Status = "INACTIVE";
                else (await db.DeliveryOrders.SingleAsync(o => o.Id == user.OrderId)).CustomerId = Guid.NewGuid();
                await db.SaveChangesAsync();
            });
            return AssistantIntent.Status;
        };
        using var response = await SendAsync(user); Assert.Equal(expected, response.StatusCode);
        await factory.WithDbAsync(async db => Assert.False(await db.AiChatSessions.AnyAsync(s => s.UserId == user.Id)));
    }

    [Fact]
    public async Task Explicit_foreign_order_cannot_be_authorized_by_owned_session()
    {
        var user = await AssistantTestData.SeedAsync(factory); var other = await AssistantTestData.SeedAsync(factory); var session = Guid.NewGuid();
        await factory.WithDbAsync(async db => { db.AiChatSessions.Add(new AiChatSession { Id = session, UserId = user.Id }); await db.SaveChangesAsync(); });
        var called = false; factory.Model.Handler = (_, _) => { called = true; return Task.FromResult(AssistantIntent.Status); };
        using var foreign = await SendAsync(user, session: session, order: other.OrderId);
        using var missing = await SendAsync(user, session: session, order: Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode); Assert.False(called);
    }

    [Theory]
    [InlineData("network")]
    [InlineData("stream")]
    [InlineData("invalid")]
    public async Task Provider_failure_or_unsupported_intent_saves_no_pair(string failure)
    {
        var user = await AssistantTestData.SeedAsync(factory);
        factory.Model.Handler = (_, _) => failure switch
        { "network" => throw new HttpRequestException("SECRET"), "stream" => throw new IOException("SECRET"), _ => Task.FromResult((AssistantIntent)999) };
        using var response = await SendAsync(user); Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("SECRET", await response.Content.ReadAsStringAsync());
        await factory.WithDbAsync(async db => Assert.False(await db.AiChatSessions.AnyAsync(s => s.UserId == user.Id)));
    }

    [Fact]
    public async Task History_is_newest_twenty_in_chronological_order_bounded_and_sanitized()
    {
        var user = await AssistantTestData.SeedAsync(factory); var session = Guid.NewGuid();
        await factory.WithDbAsync(async db =>
        {
            db.AiChatSessions.Add(new AiChatSession { Id = session, UserId = user.Id });
            for (var i = 0; i < 30; i++) db.AiChatMessages.Add(new AiChatMessage { SessionId = session, SenderType = "USER",
                MessageText = "entry " + (char)('a' + i) + " " + new string('x', 800), CreatedAt = DateTime.UtcNow.AddMinutes(i - 60) });
            db.AiChatMessages.Add(new AiChatMessage { SessionId = session, SenderType = "BOT", MessageText = "pickup code 654321", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        AssistantModelInput? seen = null; factory.Model.Handler = (input, _) => { seen = input; return Task.FromResult(AssistantIntent.Status); };
        using var response = await SendAsync(user, session: session); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(seen); Assert.True(seen.History.Length <= 20);
        Assert.True(seen.History.Sum(m => m.MessageText.EnumerateRunes().Count()) <= 12000);
        Assert.DoesNotContain(seen.History, m => m.MessageText.Contains("654321"));
        Assert.StartsWith("entry ", seen.History[0].MessageText);
        Assert.True(seen.History[0].MessageText[6] > 'j');
    }

    [Theory]
    [InlineData("The recipient address is 12 Main Street; what is the status?")]
    [InlineData("Địa chỉ là 12 Lê Lợi, trạng thái đơn thế nào?")]
    public async Task Contact_details_are_refused_before_inference_and_storage(string message)
    {
        var user = await AssistantTestData.SeedAsync(factory); var called = false;
        factory.Model.Handler = (_, _) => { called = true; return Task.FromResult(AssistantIntent.Status); };
        using var response = await SendAsync(user, message); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("REFUSED", body.GetProperty("outcome").GetString());
        Assert.False(called); Assert.DoesNotContain("Main Street", body.GetRawText()); Assert.DoesNotContain("Lê Lợi", body.GetRawText());
        await factory.WithDbAsync(async db => Assert.False(await db.AiChatSessions.AnyAsync(s => s.UserId == user.Id)));
    }

    [Fact]
    public async Task Concurrent_same_account_generation_is_rejected_but_another_account_can_continue()
    {
        var user = await AssistantTestData.SeedAsync(factory); var other = await AssistantTestData.SeedAsync(factory);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Model.Handler = async (input, ct) =>
        { if (input.Message == "first") { started.TrySetResult(); await release.Task.WaitAsync(ct); } return AssistantIntent.Status; };
        var first = SendAsync(user, "first");
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var competing = await SendAsync(user, "second"); Assert.Equal(HttpStatusCode.TooManyRequests, competing.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(5), competing.Headers.RetryAfter?.Delta);
            using var independent = await SendAsync(other, "third"); Assert.Equal(HttpStatusCode.OK, independent.StatusCode);
        }
        finally { release.TrySetResult(); }
        using var finished = await first; Assert.Equal(HttpStatusCode.OK, finished.StatusCode);
    }

    [Fact]
    public async Task Deadline_cancels_provider_saves_nothing_and_releases_slot()
    {
        var user = await AssistantTestData.SeedAsync(factory); CancellationToken providerToken = default;
        factory.Model.Handler = async (_, ct) => { providerToken = ct; await Task.Delay(Timeout.InfiniteTimeSpan, ct); return AssistantIntent.Status; };
        using var response = await SendAsync(user); Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.True(providerToken.IsCancellationRequested);
        await factory.WithDbAsync(async db => Assert.False(await db.AiChatSessions.AnyAsync(s => s.UserId == user.Id)));
        factory.Model.Handler = (_, _) => Task.FromResult(AssistantIntent.Status);
        using var recovered = await SendAsync(user); Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated_without_session_and_releases_slot()
    {
        var user = await AssistantTestData.SeedAsync(factory); using var cancellation = new CancellationTokenSource();
        factory.Model.Handler = async (_, ct) => { cancellation.Cancel(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); return AssistantIntent.Status; };
        using var scope = factory.Services.CreateScope();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.ServiceProvider.GetRequiredService<CustomerAssistantService>()
            .SendAsync(user.Id, new() { SessionId = null, OrderId = user.OrderId, Message = "status?" }, cancellation.Token));
        await factory.WithDbAsync(async db => Assert.False(await db.AiChatSessions.AnyAsync(s => s.UserId == user.Id)));
        factory.Model.Handler = (_, _) => Task.FromResult(AssistantIntent.Status);
        using var recovered = await SendAsync(user); Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Fact]
    public async Task A_competing_exchange_changes_history_and_discards_the_old_candidate()
    {
        var user = await AssistantTestData.SeedAsync(factory); var session = Guid.NewGuid();
        await factory.WithDbAsync(async db => { db.AiChatSessions.Add(new AiChatSession { Id = session, UserId = user.Id }); await db.SaveChangesAsync(); });
        factory.Model.Handler = async (_, _) =>
        {
            await factory.WithDbAsync(async db => { db.AiChatMessages.Add(new AiChatMessage { SessionId = session, SenderType = "USER", MessageText = "competing message" }); await db.SaveChangesAsync(); });
            return AssistantIntent.Status;
        };
        using var response = await SendAsync(user, session: session); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await factory.WithDbAsync(async db => Assert.Single(await db.AiChatMessages.Where(m => m.SessionId == session).ToArrayAsync()));
    }
}

public class AssistantGenerationFactory : AuthApiFactory
{
    public AssistantTestModel Model { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<CustomerAssistantOptions>(o => AssistantTestData.EnableTestPolicy(o));
            services.RemoveAll<IAssistantModelGateway>(); services.AddSingleton<IAssistantModelGateway>(Model);
        });
    }
}
public class AssistantTestModel : IAssistantModelGateway
{
    public bool IsConfigured => true;
    public Func<AssistantModelInput, CancellationToken, Task<AssistantIntent>> Handler { get; set; } = (_, _) => Task.FromResult(AssistantIntent.Status);
    public Task<AssistantIntent> ClassifyAsync(AssistantModelInput input, CancellationToken ct) => Handler(input, ct);
}
public record AssistantTestAccount(Guid Id, string Token, Guid OrderId);
public static class AssistantTestData
{
    private static int phoneSequence;
    public static void EnableTestPolicy(CustomerAssistantOptions o)
    {
        o.Enabled = true; o.ApprovedPolicyVersion = CustomerAssistantOptions.PolicyVersion; o.BackupRetentionDays = 30;
        o.ModelName = "test-only"; o.ModelRevision = "test-only-revision";
    }
    public static async Task<AssistantTestAccount> SeedAsync(WebApplicationFactory<Program> app)
    {
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await db.Roles.SingleAsync(r => r.Code == AppRoles.Customer);
        var user = new User { Email = $"assistant-{Guid.NewGuid():N}@example.com", FullName = "Private customer", PasswordHash = "unused",
            PhoneNumber = $"08{Interlocked.Increment(ref phoneSequence):D8}", Status = "ACTIVE", Role = role, RoleId = role.Id };
        var a = new LandingStation { StationCode = $"A-{Guid.NewGuid():N}"[..12], Name = "Origin", Address = "A" };
        var b = new LandingStation { StationCode = $"B-{Guid.NewGuid():N}"[..12], Name = "Destination", Address = "B" };
        var package = new Package { SenderId = user.Id, PackageName = "Parcel", WeightGram = 100 };
        var order = new DeliveryOrder { CustomerId = user.Id, PackageId = package.Id, OriginStationId = a.Id, DestinationStationId = b.Id,
            TrackingNumber = $"A-{Guid.NewGuid():N}"[..20], RecipientName = "Private recipient", RecipientPhone = "0909999999", PickupSecureCode = "654321", Status = "ARRIVED" };
        db.Users.Add(user); db.LandingStations.AddRange(a, b); db.Packages.Add(package); db.DeliveryOrders.Add(order);
        db.DeliveryStatusLogs.Add(new DeliveryStatusLog { OrderId = order.Id, FromStatus = "IN_TRANSIT", ToStatus = "ARRIVED", Remarks = "SECRET 654321" });
        await db.SaveChangesAsync();
        return new(user.Id, scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, DateTime.UtcNow), order.Id);
    }
}
