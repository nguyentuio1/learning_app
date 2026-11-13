using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class OrderReviewTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("CUSTOMER", HttpStatusCode.Forbidden)]
    [InlineData("MANAGER", HttpStatusCode.Forbidden)]
    [InlineData("STATION_OPERATOR", HttpStatusCode.Forbidden)]
    public async Task All_review_routes_require_dispatch_role(string? role, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        var actor = role is null ? null : await ReviewData.UserAsync(factory.Services, role);
        foreach (var path in new[] { "/api/dispatch/pending-orders", $"/api/delivery-orders/{Guid.NewGuid()}/approve", $"/api/delivery-orders/{Guid.NewGuid()}/reject" })
        {
            using var request = ReviewData.Request(path, actor?.Token, path.EndsWith("pending-orders") ? HttpMethod.Get : HttpMethod.Post);
            if (path.EndsWith("reject")) request.Content = JsonContent.Create(new { reason = "Closed" });
            using var response = await client.SendAsync(request);
            Assert.Equal(expected, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Theory]
    [InlineData("INACTIVE", "DISPATCHER")]
    [InlineData("ACTIVE", "CUSTOMER")]
    public async Task Persisted_status_and_role_override_stale_dispatcher_token(string status, string role)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.FindAsync(actor.Id);
            user!.Status = status;
            user.RoleId = (await db.Roles.SingleAsync(x => x.Code == role)).Id;
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        foreach (var path in new[] { "/api/dispatch/pending-orders", $"/api/delivery-orders/{Guid.NewGuid()}/approve", $"/api/delivery-orders/{Guid.NewGuid()}/reject" })
        {
            using var request = ReviewData.Request(path, actor.Token, path.EndsWith("pending-orders") ? HttpMethod.Get : HttpMethod.Post);
            if (path.EndsWith("reject")) request.Content = JsonContent.Create(new { reason = "Closed" });
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Queue_filters_across_customers_orders_ties_pages_and_projects_contract_without_writes()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var first = await ReviewData.OrderAsync(factory.Services);
        var second = await ReviewData.OrderAsync(factory.Services);
        var at = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        await factory.WithDbAsync(async db =>
        {
            var a = await db.DeliveryOrders.FindAsync(first.Id);
            var b = await db.DeliveryOrders.FindAsync(second.Id);
            a!.CreatedAt = b!.CreatedAt = at;
            b.OriginStationId = a.OriginStationId;
            b.DestinationStationId = a.DestinationStationId;
            await db.SaveChangesAsync();
        });
        await ReviewData.OrderAsync(factory.Services, "APPROVED", first.OriginStationId, first.DestinationStationId, at);
        await ReviewData.OrderAsync(factory.Services, "PENDING", first.OriginStationId, first.DestinationStationId, at.AddSeconds(1));
        await ReviewData.OrderAsync(factory.Services, "PENDING", first.OriginStationId, null, at);
        using var client = factory.CreateClient();
        var query = $"/api/dispatch/pending-orders?originStationId={first.OriginStationId}&destinationStationId={first.DestinationStationId}&fromDate=2026-10-01T00:00:00Z&toDate=2026-10-01T00:00:00Z&pageSize=1&page=2";
        using var request = ReviewData.Request(query, actor.Token, HttpMethod.Get);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, json.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, json.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, json.GetProperty("page").GetInt32());
        Assert.Equal(1, json.GetProperty("pageSize").GetInt32());
        var row = Assert.Single(json.GetProperty("items").EnumerateArray());
        var expected = new[] { first, second }.OrderBy(o => o.Id).Last();
        Assert.Equal(expected.Id, row.GetProperty("orderId").GetGuid());
        Assert.Equal("Review customer", row.GetProperty("customerName").GetString());
        Assert.Equal(expected.Customer.PhoneNumber, row.GetProperty("customerPhone").GetString());
        Assert.Equal("Books", row.GetProperty("packageName").GetString());
        Assert.Equal(450, row.GetProperty("weightGram").GetDecimal());
        Assert.Equal(25, row.GetProperty("lengthCm").GetDecimal());
        Assert.Equal(18, row.GetProperty("widthCm").GetDecimal());
        Assert.Equal(10, row.GetProperty("heightCm").GetDecimal());
        Assert.True(row.GetProperty("isFragile").GetBoolean());
        Assert.Equal(first.OriginStationId, row.GetProperty("originStation").GetProperty("id").GetGuid());
        Assert.Equal(first.DestinationStationId, row.GetProperty("destinationStation").GetProperty("id").GetGuid());
        Assert.Equal("Origin", row.GetProperty("originStation").GetProperty("name").GetString());
        Assert.True(row.GetProperty("originStation").TryGetProperty("stationCode", out _));
        Assert.EndsWith("Z", row.GetProperty("createdAt").GetString());
        Assert.DoesNotContain("pickup", row.GetRawText(), StringComparison.OrdinalIgnoreCase);
        using var emptyRequest = ReviewData.Request(query.Replace("page=2", "page=999"), actor.Token, HttpMethod.Get);
        using var emptyResponse = await client.SendAsync(emptyRequest);
        var empty = await emptyResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        Assert.Equal(2, empty.GetProperty("totalCount").GetInt32());
        // An unrelated station UUID yields an empty queue with default paging.
        using var defaultsRequest = ReviewData.Request($"/api/dispatch/pending-orders?originStationId={Guid.NewGuid()}", actor.Token, HttpMethod.Get);
        using var defaultsResponse = await client.SendAsync(defaultsRequest);
        var defaults = await defaultsResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(defaults.GetProperty("items").EnumerateArray());
        Assert.Equal(1, defaults.GetProperty("page").GetInt32());
        Assert.Equal(20, defaults.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, defaults.GetProperty("totalPages").GetInt32());
        await ReviewData.AssertEffectsAsync(factory.Services, first.Id, "PENDING", 0, 0);
        await ReviewData.AssertEffectsAsync(factory.Services, second.Id, "PENDING", 0, 0);
    }

    [Theory]
    [InlineData("originStationId=bad")]
    [InlineData("destinationStationId=bad")]
    [InlineData("fromDate=bad")]
    [InlineData("toDate=bad")]
    [InlineData("fromDate=2026-10-02T00:00:00Z&toDate=2026-10-01T00:00:00Z")]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task Queue_rejects_invalid_filters(string query)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        using var client = factory.CreateClient();
        using var request = ReviewData.Request("/api/dispatch/pending-orders?" + query, actor.Token, HttpMethod.Get);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("errors", out _));
    }

    [Theory]
    [InlineData("DISPATCHER")]
    [InlineData("ADMIN")]
    public async Task Approval_sets_dispatcher_and_atomic_effects_without_schedule_or_mission(string role)
    {
        var actor = await ReviewData.UserAsync(factory.Services, role);
        var order = await ReviewData.OrderAsync(factory.Services);
        using var client = factory.CreateClient();
        using var request = ReviewData.Request($"/api/delivery-orders/{order.Id}/approve", actor.Token);
        request.Headers.UserAgent.ParseAdd("ReviewAcceptance/1.0");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(order.Id, json.GetProperty("orderId").GetGuid());
        Assert.Equal(order.TrackingNumber, json.GetProperty("trackingNumber").GetString());
        Assert.Equal("APPROVED", json.GetProperty("status").GetString());
        Assert.EndsWith("Z", json.GetProperty("updatedAt").GetString());
        Assert.Equal(4, json.EnumerateObject().Count());
        await ReviewData.AssertEffectsAsync(factory.Services, order.Id, "APPROVED", 1, 1);
        await factory.WithDbAsync(async db =>
        {
            var saved = await db.DeliveryOrders.FindAsync(order.Id);
            Assert.Equal(actor.Id, saved!.DispatcherId);
            Assert.Null(saved.ScheduledDepartureTime);
            Assert.Null(saved.EstimatedDeliveryTime);
            Assert.False(await db.FlightMissions.AnyAsync(x => x.OrderId == order.Id));
            var log = await db.DeliveryStatusLogs.SingleAsync(x => x.OrderId == order.Id);
            Assert.Equal("PENDING", log.FromStatus);
            Assert.Equal(actor.Id, log.ChangedBy);
            Assert.Equal(saved.UpdatedAt, log.CreatedAt);
            var notification = await db.Notifications.SingleAsync(x => x.OrderId == order.Id);
            Assert.Equal(order.CustomerId, notification.UserId);
            Assert.Equal("ORDER_STATUS_CHANGED", notification.Type);
            Assert.Equal(log.CreatedAt, notification.CreatedAt);
            var audit = await db.AuditLogs.SingleAsync(x => x.EntityId == order.Id.ToString());
            Assert.Equal("ORDER_APPROVAL", audit.Action);
            Assert.Equal(actor.Id, audit.UserId);
            Assert.Equal("DeliveryOrder", audit.EntityName);
            Assert.Equal("ReviewAcceptance/1.0", audit.UserAgent);
        });
        using var repeat = ReviewData.Request($"/api/delivery-orders/{order.Id}/approve", actor.Token);
        using var repeated = await client.SendAsync(repeat);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        await ReviewData.AssertEffectsAsync(factory.Services, order.Id, "APPROVED", 1, 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Approval_names_nonoperational_station_and_writes_nothing(bool origin)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services);
        await factory.WithDbAsync(async db =>
        {
            var station = await db.LandingStations.FindAsync(origin ? order.OriginStationId : order.DestinationStationId);
            station!.Status = "MAINTENANCE";
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        using var request = ReviewData.Request($"/api/delivery-orders/{order.Id}/approve", actor.Token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(origin ? "Origin" : "Destination", await response.Content.ReadAsStringAsync());
        await ReviewData.AssertEffectsAsync(factory.Services, order.Id, "PENDING", 0, 0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("LONG")]
    public async Task Rejection_validates_trimmed_reason_without_effects(string? reason)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services);
        using var client = factory.CreateClient();
        using var request = ReviewData.Request($"/api/delivery-orders/{order.Id}/reject", actor.Token);
        request.Content = JsonContent.Create(new { reason = reason == "LONG" ? new string('x', 501) : reason });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("reason", out _));
        await ReviewData.AssertEffectsAsync(factory.Services, order.Id, "PENDING", 0, 0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public async Task Rejection_trims_reason_and_cancels_with_history_notification_and_audit(int length)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "ADMIN");
        var order = await ReviewData.OrderAsync(factory.Services);
        using var client = factory.CreateClient();
        using var request = ReviewData.Request($"/api/delivery-orders/{order.Id}/reject", actor.Token);
        request.Content = JsonContent.Create(new { reason = "  " + new string('x', length) + "  " });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("CANCELLED", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        await ReviewData.AssertEffectsAsync(factory.Services, order.Id, "CANCELLED", 1, 1);
        await factory.WithDbAsync(async db =>
        {
            var saved = await db.DeliveryOrders.FindAsync(order.Id);
            Assert.Equal(new string('x', length), saved!.CancelReason);
            Assert.Null(saved.DispatcherId);
            var log = await db.DeliveryStatusLogs.SingleAsync(x => x.OrderId == order.Id);
            Assert.Equal(saved.CancelReason, log.Remarks);
            Assert.Equal(actor.Id, log.ChangedBy);
            Assert.Equal("ORDER_REJECTION", (await db.AuditLogs.SingleAsync(x => x.EntityId == order.Id.ToString())).Action);
        });
    }

    [Theory]
    [InlineData("APPROVED")]
    [InlineData("CANCELLED")]
    [InlineData("DELIVERED")]
    [InlineData("FAILED")]
    [InlineData("IN_TRANSIT")]
    [InlineData("ARRIVED")]
    [InlineData("READY_FOR_TAKEOFF")]
    public async Task Both_decisions_refuse_nonpending_orders_without_effects(string status)
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services, status);
        using var client = factory.CreateClient();
        foreach (var action in new[] { "approve", "reject" })
        {
            using var request = ReviewData.Request($"/api/delivery-orders/{order.Id}/{action}", actor.Token);
            if (action == "reject") request.Content = JsonContent.Create(new { reason = "Closed" });
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        await ReviewData.AssertEffectsAsync(factory.Services, order.Id, status, 0, 0);
    }

    [Fact]
    public async Task Unknown_order_is_404_for_both_decisions()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        using var client = factory.CreateClient();
        foreach (var action in new[] { "approve", "reject" })
        {
            using var request = ReviewData.Request($"/api/delivery-orders/{Guid.NewGuid()}/{action}", actor.Token);
            if (action == "reject") request.Content = JsonContent.Create(new { reason = "Closed" });
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task Racing_rejection_and_customer_cancel_have_one_winner_and_no_duplicate_effects()
    {
        var actor = await ReviewData.UserAsync(factory.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(factory.Services);
        var customer = await ReviewData.TokenAsync(factory.Services, order.CustomerId);
        using var client = factory.CreateClient();
        using var reject = ReviewData.Request($"/api/delivery-orders/{order.Id}/reject", actor.Token);
        reject.Content = JsonContent.Create(new { reason = "Closed" });
        using var cancel = ReviewData.Request($"/api/delivery-orders/{order.Id}/cancel", customer);
        cancel.Content = JsonContent.Create(new { cancellationReason = "No longer needed" });
        var results = await Task.WhenAll(client.SendAsync(reject), client.SendAsync(cancel));
        try
        {
            Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
            await ReviewData.AssertEffectsAsync(factory.Services, order.Id, "CANCELLED", 1, results[0].IsSuccessStatusCode ? 1 : 0);
        }
        finally { foreach (var response in results) response.Dispose(); }
    }

    [Fact]
    public async Task Save_failure_does_not_persist_review_status_or_effects()
    {
        using var failing = new FailingReviewFactory();
        using var client = failing.CreateClient();
        var actor = await ReviewData.UserAsync(failing.Services, "DISPATCHER");
        var order = await ReviewData.OrderAsync(failing.Services);
        using var request = ReviewData.Request($"/api/delivery-orders/{order.Id}/approve", actor.Token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await ReviewData.AssertEffectsAsync(failing.Services, order.Id, "PENDING", 0, 0);
    }

    private sealed class FailingReviewFactory : AuthApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddScoped(sp => new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>(sp.GetRequiredService<DbContextOptions<AppDbContext>>())
                    .AddInterceptors(new FailReviewSave()).Options)));
        }
    }

    private sealed class FailReviewSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added && x.Entity.Action == "ORDER_APPROVAL"))
                throw new DbUpdateException("Injected review persistence failure");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}

internal static class ReviewData
{
    internal sealed record Actor(Guid Id, string Token);
    public static async Task<Actor> UserAsync(IServiceProvider services, string role)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unique = Guid.NewGuid().ToString("N");
        var user = new User { Role = await db.Roles.SingleAsync(x => x.Code == role), Email = $"review-{unique}@example.com", PhoneNumber = unique[..15], FullName = "Review customer" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new(user.Id, scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, DateTime.UtcNow));
    }

    public static async Task<string> TokenAsync(IServiceProvider services, Guid userId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.Include(x => x.Role).SingleAsync(x => x.Id == userId);
        return scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, DateTime.UtcNow);
    }

    public static async Task<DeliveryOrder> OrderAsync(IServiceProvider services, string status = "PENDING", Guid? originId = null, Guid? destinationId = null, DateTime? createdAt = null)
    {
        var customer = await UserAsync(services, "CUSTOMER");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unique = Guid.NewGuid().ToString("N");
        var order = new DeliveryOrder
        {
            Customer = (await db.Users.FindAsync(customer.Id))!, Status = status, TrackingNumber = $"R-{unique}", PickupSecureCode = "SECRET",
            CreatedAt = createdAt ?? DateTime.UtcNow,
            Package = new Package { SenderId = customer.Id, PackageName = "Books", WeightGram = 450, LengthCm = 25, WidthCm = 18, HeightCm = 10, IsFragile = true }
        };
        if (originId.HasValue) order.OriginStationId = originId.Value;
        else order.OriginStation = new LandingStation { StationCode = $"O-{unique[..8]}", Name = "Origin" };
        if (destinationId.HasValue) order.DestinationStationId = destinationId.Value;
        else order.DestinationStation = new LandingStation { StationCode = $"D-{unique[..8]}", Name = "Destination" };
        db.DeliveryOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    public static HttpRequestMessage Request(string path, string? token, HttpMethod? method = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Post, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    public static async Task AssertEffectsAsync(IServiceProvider services, Guid id, string status, int transitions, int audits)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(status, (await db.DeliveryOrders.FindAsync(id))!.Status);
        Assert.Equal(transitions, await db.DeliveryStatusLogs.CountAsync(x => x.OrderId == id));
        Assert.Equal(transitions, await db.Notifications.CountAsync(x => x.OrderId == id));
        Assert.Equal(audits, await db.AuditLogs.CountAsync(x => x.EntityId == id.ToString()));
    }
}
