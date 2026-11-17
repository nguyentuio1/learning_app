using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class AssistantPostgresTests
{
    [PostgresFact]
    public async Task Successful_pair_is_atomic_with_no_model_transaction_and_post_insert_failure_rolls_back_new_session()
    {
        using var fixture = new PostgresApiFactory(); var model = new AssistantTestModel(); var fault = new FailAfterBotInsert();
        using var app = fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<CustomerAssistantOptions>(AssistantTestData.EnableTestPolicy);
            services.RemoveAll<IAssistantModelGateway>(); services.AddSingleton<IAssistantModelGateway>(model);
            foreach (var item in services.Where(s => s.ImplementationType == typeof(AssistantRetentionWorker)).ToArray()) services.Remove(item);
            // EF8 retains the first options registration; explicitly replace it to attach the failure injector.
            services.RemoveAll<DbContextOptions<AppDbContext>>(); services.RemoveAll<DbContextOptions>();
            services.AddDbContext<AppDbContext>(o => o.UseNpgsql(fixture.DatabaseConnectionString).AddInterceptors(fault));
        }));
        using var client = app.CreateClient(); var user = await AssistantTestData.SeedAsync(app);
        model.Handler = async (_, ct) =>
        {
            await using var connection = new NpgsqlConnection(fixture.DatabaseConnectionString); await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND state='idle in transaction'";
            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync(ct))!); return AssistantIntent.Status;
        };
        using var scope = app.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<CustomerAssistantService>();
        var result = await service.SendAsync(user.Id, new() { SessionId = null, OrderId = user.OrderId, Message = "status?" }, default);
        Assert.Equal("ANSWERED", result.Outcome);
        Assert.True(fault.Saves >= 3); // Verify the injector is attached before claiming a rollback test.
        using (var checking = app.Services.CreateScope())
        {
            var db = checking.ServiceProvider.GetRequiredService<AppDbContext>();
            var messages = await db.AiChatMessages.Where(m => m.SessionId == result.SessionId).OrderBy(m => m.Id).ToArrayAsync();
            Assert.Equal(new[] { "USER", "BOT" }, messages.Select(m => m.SenderType));
            Assert.Equal(result.UserMessageId, messages[0].Id.ToString()); Assert.Equal(result.Reply.Id, messages[1].Id.ToString());
        }
        var failedUser = await AssistantTestData.SeedAsync(app); fault.Enabled = true;
        using (var failedScope = app.Services.CreateScope())
        {
            var failure = await Assert.ThrowsAsync<ApiException>(() => failedScope.ServiceProvider.GetRequiredService<CustomerAssistantService>()
                .SendAsync(failedUser.Id, new() { SessionId = null, OrderId = failedUser.OrderId, Message = "status?" }, default));
            Assert.Equal(503, failure.StatusCode); Assert.True(fault.SawPersistedBot);
        }
        using (var checking = app.Services.CreateScope())
        {
            var db = checking.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.AiChatSessions.AnyAsync(s => s.UserId == failedUser.Id)); Assert.Equal(2, await db.AiChatMessages.CountAsync());
            Assert.Equal("ARRIVED", (await db.DeliveryOrders.SingleAsync(o => o.Id == failedUser.OrderId)).Status);
        }
    }

    [PostgresFact]
    public async Task Changed_logs_or_session_ownership_after_generation_cannot_commit_a_pair()
    {
        using var fixture = new PostgresApiFactory(); var model = new AssistantTestModel();
        using var app = fixture.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.PostConfigure<CustomerAssistantOptions>(AssistantTestData.EnableTestPolicy);
            services.RemoveAll<IAssistantModelGateway>(); services.AddSingleton<IAssistantModelGateway>(model);
        }));
        using var client = app.CreateClient(); var user = await AssistantTestData.SeedAsync(app); var other = await AssistantTestData.SeedAsync(app);
        model.Handler = async (_, ct) =>
        {
            using var editing = app.Services.CreateScope(); var db = editing.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.DeliveryStatusLogs.Where(l => l.OrderId == user.OrderId).ExecuteUpdateAsync(s => s.SetProperty(l => l.ToStatus, "FAILED"), ct);
            return AssistantIntent.Status;
        };
        using (var scope = app.Services.CreateScope())
        {
            var error = await Assert.ThrowsAsync<ApiException>(() => scope.ServiceProvider.GetRequiredService<CustomerAssistantService>()
                .SendAsync(user.Id, new() { SessionId = null, OrderId = user.OrderId, Message = "status?" }, default));
            Assert.Equal(409, error.StatusCode);
        }
        var session = Guid.NewGuid();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.AiChatSessions.Add(new AiChatSession { Id = session, UserId = user.Id }); await db.SaveChangesAsync();
        }
        model.Handler = async (_, ct) =>
        {
            using var editing = app.Services.CreateScope(); var db = editing.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.AiChatSessions.Where(s => s.Id == session).ExecuteUpdateAsync(s => s.SetProperty(s => s.UserId, other.Id), ct); return AssistantIntent.Status;
        };
        using (var scope = app.Services.CreateScope())
        {
            var error = await Assert.ThrowsAsync<ApiException>(() => scope.ServiceProvider.GetRequiredService<CustomerAssistantService>()
                .SendAsync(user.Id, new() { SessionId = session, OrderId = user.OrderId, Message = "status?" }, default));
            Assert.Equal(404, error.StatusCode); Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().AiChatMessages.ToArrayAsync());
        }
    }

    [PostgresFact]
    public async Task Owned_deletion_and_approved_retention_really_cascade_in_postgres()
    {
        using var fixture = new PostgresApiFactory();
        using var app = fixture.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.PostConfigure<CustomerAssistantOptions>(AssistantTestData.EnableTestPolicy);
            foreach (var item in services.Where(s => s.ImplementationType == typeof(AssistantRetentionWorker)).ToArray()) services.Remove(item);
        }));
        using var client = app.CreateClient(); var user = await AssistantTestData.SeedAsync(app);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owned = new AiChatSession { UserId = user.Id }; var expired = new AiChatSession { UserId = user.Id, CreatedAt = DateTime.UtcNow.AddDays(-31) };
        db.AiChatSessions.AddRange(owned, expired); db.AiChatMessages.AddRange(
            new AiChatMessage { SessionId = owned.Id, SenderType = "USER", MessageText = "status?" },
            new AiChatMessage { SessionId = expired.Id, SenderType = "USER", MessageText = "status?" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var retention = scope.ServiceProvider.GetRequiredService<AssistantRetentionService>();
        await retention.DeleteAsync(user.Id, owned.Id, default); Assert.False(await db.AiChatMessages.AnyAsync(m => m.SessionId == owned.Id));
        Assert.Equal(1, await retention.CleanupBatchAsync(default)); Assert.Empty(await db.AiChatMessages.ToArrayAsync());
    }

    private class FailAfterBotInsert : SaveChangesInterceptor
    {
        public bool Enabled; public bool SawPersistedBot; public int Saves;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
        {
            Saves++;
            if (Enabled && eventData.Context!.ChangeTracker.Entries<AiChatMessage>().Any(e => e.Entity.SenderType == "BOT"))
            { Enabled = false; SawPersistedBot = true; throw new DbUpdateException("Injected failure after the BOT insert, before outer commit."); }
            return ValueTask.FromResult(result);
        }
    }
}
