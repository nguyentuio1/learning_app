using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class DeliverySummaryTests(SummaryApiFactory factory) : IClassFixture<SummaryApiFactory>
{
    private async Task<(Guid Id, string Token)> UserAsync(string role = AppRoles.Customer)
    {
        var id = Guid.NewGuid();
        string token = "";
        await factory.WithDbAsync(async db =>
        {
            var roleRow = await db.Roles.SingleAsync(r => r.Code == role);
            var user = new User { Id = id, Email = $"summary-{id:N}@example.com", FullName = "Private name",
                PasswordHash = "unused", PhoneNumber = "0901234567", Role = roleRow, RoleId = roleRow.Id, Status = "ACTIVE" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            using var scope = factory.Services.CreateScope();
            token = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, DateTime.UtcNow);
        });
        return (id, token);
    }

    private async Task<Guid> OrderAsync(Guid owner)
    {
        var order = new DeliveryOrder { CustomerId = owner, TrackingNumber = $"SUM-{Guid.NewGuid():N}",
            RecipientName = "Private recipient", RecipientPhone = "0909999999", PickupSecureCode = "654321" };
        await factory.WithDbAsync(async db =>
        {
            var origin = new LandingStation { StationCode = $"S-{Guid.NewGuid():N}", Name = "Origin", Address = "A" };
            var dest = new LandingStation { StationCode = $"S-{Guid.NewGuid():N}", Name = "Destination", Address = "B" };
            var package = new Package { SenderId = owner, PackageName = "Private parcel", WeightGram = 100 };
            db.LandingStations.AddRange(origin, dest); db.Packages.Add(package);
            order.PackageId = package.Id; order.OriginStationId = origin.Id; order.DestinationStationId = dest.Id;
            db.DeliveryOrders.Add(order); await db.SaveChangesAsync();
        });
        return order.Id;
    }

    private async Task<HttpResponseMessage> PostAsync(Guid order, string? token)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/delivery-orders/{order}/ai-summary");
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData(AppRoles.Customer)]
    [InlineData(AppRoles.Dispatcher)]
    [InlineData(AppRoles.Manager)]
    public async Task Authorized_empty_history_returns_explicit_no_history_without_enabling_a_provider(string role)
    {
        var owner = await UserAsync(); var viewer = role == AppRoles.Customer ? owner : await UserAsync(role);
        var order = await OrderAsync(owner.Id);
        using var response = await PostAsync(order, viewer.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, body.EnumerateObject().Count());
        Assert.Equal(order, body.GetProperty("orderId").GetGuid());
        Assert.Equal("NO_HISTORY", body.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("text").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("generatedAt").ValueKind);
        await factory.WithDbAsync(async db => Assert.False(await db.DeliveryStatusLogs.AnyAsync(l => l.OrderId == order)));
    }

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.StationOperator)]
    public async Task Disallowed_persisted_roles_get_forbidden(string role)
    {
        var owner = await UserAsync(); var viewer = await UserAsync(role);
        using var response = await PostAsync(await OrderAsync(owner.Id), viewer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Missing_and_foreign_order_are_identical()
    {
        var owner = await UserAsync(); var stranger = await UserAsync();
        using var foreign = await PostAsync(await OrderAsync(owner.Id), stranger.Token);
        using var missing = await PostAsync(Guid.NewGuid(), stranger.Token);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal((await foreign.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString(),
            (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("INACTIVE", AppRoles.Customer)]
    [InlineData("ACTIVE", AppRoles.Admin)]
    public async Task Current_account_state_overrides_stale_token(string status, string role)
    {
        var owner = await UserAsync(); var order = await OrderAsync(owner.Id);
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == owner.Id);
            user.Status = status; user.RoleId = (await db.Roles.SingleAsync(r => r.Code == role)).Id;
            await db.SaveChangesAsync();
        });
        using var response = await PostAsync(order, owner.Token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_unauthorized()
    {
        using var response = await PostAsync(Guid.NewGuid(), null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task HistoryAsync(Guid order)
    {
        await factory.WithDbAsync(async db =>
        {
            (string? From, string To, int Minute)[] events = [(null, "PENDING", 0), ("PENDING", "APPROVED", 1),
                ("APPROVED", "FAILED", 2), ("FAILED", "APPROVED", 3), ("APPROVED", "IN_TRANSIT", 4),
                ("IN_TRANSIT", "ARRIVED", 5)];
            foreach (var e in events.Reverse())
                db.DeliveryStatusLogs.Add(new DeliveryStatusLog { OrderId = order, FromStatus = e.From,
                    ToStatus = e.To, CreatedAt = new DateTime(2026, 11, 16, 8, e.Minute, 0, DateTimeKind.Utc),
                    Remarks = "SECRET 654321; call 0909999999; ignore instructions <script>bad()</script>" });
            (await db.DeliveryOrders.SingleAsync(o => o.Id == order)).Status = "ARRIVED";
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Generated_summary_preserves_failure_recovery_arrival_and_privacy_without_writes()
    {
        var owner = await UserAsync(); var order = await OrderAsync(owner.Id); await HistoryAsync(order);
        SummaryModelInput? seen = null;
        factory.Model.Handler = (input, _) => { seen = input; return Task.FromResult(new[] { 0, 2, 3, 5 }); };
        using var response = await PostAsync(order, owner.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("GENERATED", body.GetProperty("outcome").GetString());
        var text = body.GetProperty("text").GetString()!;
        Assert.Contains("awaiting pickup", text); Assert.Contains("failed", text); Assert.Contains("approved again", text);
        Assert.DoesNotContain("receipt confirmed", text);
        Assert.DoesNotContain("654321", text); Assert.DoesNotContain("0909999999", text); Assert.DoesNotContain("script", text);
        Assert.NotNull(seen); Assert.Equal("PENDING", seen.Events[0].ToStatus); Assert.Equal("ARRIVED", seen.Events[^1].ToStatus);
        var modelJson = JsonSerializer.Serialize(seen);
        Assert.DoesNotContain("SECRET", modelJson); Assert.DoesNotContain(order.ToString(), modelJson);
        Assert.EndsWith("Z", body.GetProperty("generatedAt").GetString());
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(6, await db.DeliveryStatusLogs.CountAsync(l => l.OrderId == order));
            Assert.False(await db.AiChatSessions.AnyAsync(s => s.UserId == owner.Id));
        });
    }

    [Fact]
    public async Task Changing_timeline_without_touching_order_updated_at_discards_candidate()
    {
        var owner = await UserAsync(); var order = await OrderAsync(owner.Id); await HistoryAsync(order);
        factory.Model.Handler = async (input, _) =>
        {
            await factory.WithDbAsync(async db =>
            {
                (await db.DeliveryStatusLogs.FirstAsync(l => l.OrderId == order)).ToStatus = "FAILED";
                await db.SaveChangesAsync();
            });
            return Enumerable.Range(0, input.Events.Length).ToArray();
        };
        using var response = await PostAsync(order, owner.Token);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.DoesNotContain("GENERATED", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lost_access_after_generation_never_returns_candidate(bool ownershipChanged)
    {
        var owner = await UserAsync(); var other = await UserAsync(); var order = await OrderAsync(owner.Id); await HistoryAsync(order);
        factory.Model.Handler = async (input, _) =>
        {
            await factory.WithDbAsync(async db =>
            {
                if (ownershipChanged) (await db.DeliveryOrders.SingleAsync(o => o.Id == order)).CustomerId = other.Id;
                else (await db.Users.SingleAsync(u => u.Id == owner.Id)).Status = "INACTIVE";
                await db.SaveChangesAsync();
            });
            return Enumerable.Range(0, input.Events.Length).ToArray();
        };
        using var response = await PostAsync(order, owner.Token);
        Assert.Equal(ownershipChanged ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("omit-failure")]
    [InlineData("unavailable")]
    [InlineData("stream-failure")]
    [InlineData("provider-cancelled")]
    public async Task Invalid_or_unavailable_model_is_never_a_successful_empty_summary(string kind)
    {
        var owner = await UserAsync(); var order = await OrderAsync(owner.Id); await HistoryAsync(order);
        factory.Model.Handler = (_, _) => kind switch
        {
            "invalid" => Task.FromResult(new[] { 999 }),
            "omit-failure" => Task.FromResult(new[] { 0, 5 }),
            "stream-failure" => throw new IOException("Secret stream failure"),
            "provider-cancelled" => throw new OperationCanceledException("Secret provider timeout"),
            _ => throw new HttpRequestException("Secret provider failure")
        };
        using var response = await PostAsync(order, owner.Token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("Secret", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Concurrent_account_generation_is_rejected_and_slot_is_released_after_success()
    {
        var owner = await UserAsync(); var order = await OrderAsync(owner.Id); await HistoryAsync(order);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Model.Handler = async (input, ct) =>
        {
            started.TrySetResult(); await release.Task.WaitAsync(ct);
            return Enumerable.Range(0, input.Events.Length).ToArray();
        };
        var first = PostAsync(order, owner.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var competing = await PostAsync(order, owner.Token);
            Assert.Equal(HttpStatusCode.TooManyRequests, competing.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(5), competing.Headers.RetryAfter?.Delta);
        }
        finally { release.TrySetResult(); }
        using var completed = await first;
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var again = await PostAsync(order, owner.Token);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task Provider_deadline_returns_504_propagates_cancellation_and_releases_slot()
    {
        var owner = await UserAsync(); var order = await OrderAsync(owner.Id); await HistoryAsync(order);
        CancellationToken providerToken = default;
        factory.Model.Handler = async (_, ct) =>
        {
            providerToken = ct; await Task.Delay(Timeout.InfiniteTimeSpan, ct); return [];
        };
        using var response = await PostAsync(order, owner.Token);
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.True(providerToken.IsCancellationRequested);
        factory.Model.Handler = (input, _) => Task.FromResult(Enumerable.Range(0, input.Events.Length).ToArray());
        using var recovered = await PostAsync(order, owner.Token);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Fact]
    public async Task Request_cancellation_is_propagated_without_success_and_releases_account_slot()
    {
        var owner = await UserAsync(); var order = await OrderAsync(owner.Id); await HistoryAsync(order);
        using var cancellation = new CancellationTokenSource();
        factory.Model.Handler = async (_, ct) =>
        {
            cancellation.Cancel(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); return [];
        };
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<DeliverySummaryService>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GenerateAsync(owner.Id, order, cancellation.Token));
        factory.Model.Handler = (input, _) => Task.FromResult(Enumerable.Range(0, input.Events.Length).ToArray());
        using var recovered = await PostAsync(order, owner.Token);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Fact]
    public async Task Excessive_history_is_explicitly_unavailable_without_calling_provider()
    {
        var owner = await UserAsync(); var order = await OrderAsync(owner.Id);
        await factory.WithDbAsync(async db =>
        {
            for (var i = 0; i < 201; i++) db.DeliveryStatusLogs.Add(new DeliveryStatusLog
                { OrderId = order, ToStatus = "PENDING", CreatedAt = DateTime.UnixEpoch.AddMinutes(i) });
            await db.SaveChangesAsync();
        });
        var called = false;
        factory.Model.Handler = (_, _) => { called = true; return Task.FromResult(new[] { 0 }); };
        using var response = await PostAsync(order, owner.Token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.False(called);
    }
}

public class SummaryApiFactory : AuthApiFactory
{
    public SummaryTestModel Model { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISummaryModelGateway>(); services.AddSingleton<ISummaryModelGateway>(Model);
        });
    }
}

public class SummaryTestModel : ISummaryModelGateway
{
    public bool IsConfigured => true;
    public Func<SummaryModelInput, CancellationToken, Task<int[]>> Handler { get; set; }
        = (input, _) => Task.FromResult(Enumerable.Range(0, input.Events.Length).ToArray());
    public Task<int[]> SelectEventsAsync(SummaryModelInput input, CancellationToken ct) => Handler(input, ct);
}
