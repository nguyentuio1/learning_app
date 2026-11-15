using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>LA-326: Customer notifications API (docs/api/notification-api.md).</summary>
public class NotificationApiTests : IClassFixture<AuthApiFactory>, IDisposable
{
    private const string Password = "Passw0rd123";
    private static readonly string PasswordHash = new PasswordHasher().Hash(Password);
    private static int _counter;
    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public NotificationApiTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private async Task<(Guid UserId, string Token)> SeedUserAsync(string role = AppRoles.Customer, string status = "ACTIVE")
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"notifuser{n}@example.com";
        var userId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            var roleEntity = await db.Roles.SingleAsync(r => r.Code == role);
            db.Users.Add(new User
            {
                Id = userId,
                Email = email,
                FullName = $"Notification User {n}",
                PhoneNumber = $"09{n:D8}",
                PasswordHash = PasswordHash,
                RoleId = roleEntity.Id,
                Status = status
            });
            await db.SaveChangesAsync();
        });

        if (status != "ACTIVE")
        {
            // Login fails for inactive users, so generate a token directly via TokenService
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.Include(u => u.Role).SingleAsync(u => u.Id == userId);
            var token = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, DateTime.UtcNow);
            return (userId, token);
        }

        using var loginRes = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var doc = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        return (userId, doc.GetProperty("accessToken").GetString()!);
    }

    private void SetToken(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<Notification> SeedNotificationAsync(Guid userId, string title, string message, bool isRead = false, Guid? orderId = null, DateTime? createdAt = null)
    {
        var notif = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrderId = orderId,
            Type = "ORDER_STATUS_CHANGED",
            Title = title,
            Message = message,
            IsRead = isRead,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            ReadAt = isRead ? (createdAt ?? DateTime.UtcNow) : null
        };

        await _factory.WithDbAsync(async db =>
        {
            db.Notifications.Add(notif);
            await db.SaveChangesAsync();
        });

        return notif;
    }

    [Fact]
    public async Task List_ReturnsPagedResult_NewestFirst_AndIsolatesUsers()
    {
        var (userA, tokenA) = await SeedUserAsync();
        var (userB, _) = await SeedUserAsync();

        var baseTime = DateTime.UtcNow.AddHours(-10);
        await SeedNotificationAsync(userA, "Older", "Message 1", isRead: false, createdAt: baseTime);
        await SeedNotificationAsync(userA, "Newer", "Message 2", isRead: true, createdAt: baseTime.AddHours(2));
        await SeedNotificationAsync(userB, "UserB Note", "Other user message", isRead: false);

        SetToken(tokenA);
        using var res = await _client.GetAsync("/api/notifications?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var page = await res.Content.ReadFromJsonAsync<PagedResult<NotificationResponseDto>>();
        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.Equal(2, page.Items.Count);

        // Newest first
        Assert.Equal("Newer", page.Items[0].Title);
        Assert.True(page.Items[0].IsRead);
        Assert.Equal("Older", page.Items[1].Title);
        Assert.False(page.Items[1].IsRead);
    }

    [Fact]
    public async Task List_FiltersByIsRead()
    {
        var (user, token) = await SeedUserAsync();
        await SeedNotificationAsync(user, "Unread 1", "M1", isRead: false);
        await SeedNotificationAsync(user, "Read 1", "M2", isRead: true);
        await SeedNotificationAsync(user, "Unread 2", "M3", isRead: false);

        SetToken(token);

        // Filter isRead=false
        using var unreadRes = await _client.GetAsync("/api/notifications?isRead=false");
        Assert.Equal(HttpStatusCode.OK, unreadRes.StatusCode);
        var unreadPage = await unreadRes.Content.ReadFromJsonAsync<PagedResult<NotificationResponseDto>>();
        Assert.NotNull(unreadPage);
        Assert.Equal(2, unreadPage.TotalCount);
        Assert.All(unreadPage.Items, n => Assert.False(n.IsRead));

        // Filter isRead=true
        using var readRes = await _client.GetAsync("/api/notifications?isRead=true");
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);
        var readPage = await readRes.Content.ReadFromJsonAsync<PagedResult<NotificationResponseDto>>();
        Assert.NotNull(readPage);
        Assert.Equal(1, readPage.TotalCount);
        Assert.All(readPage.Items, n => Assert.True(n.IsRead));
    }

    [Fact]
    public async Task List_ReturnsEmptyList_WhenNoNotifications()
    {
        var (_, token) = await SeedUserAsync();
        SetToken(token);

        using var res = await _client.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var page = await res.Content.ReadFromJsonAsync<PagedResult<NotificationResponseDto>>();
        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    public async Task List_RejectsInvalidPaging_With400ProblemDetails(string query, string field)
    {
        var (_, token) = await SeedUserAsync();
        SetToken(token);

        using var res = await _client.GetAsync($"/api/notifications?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(doc.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsAccurateCount_ForCallerOnly()
    {
        var (userA, tokenA) = await SeedUserAsync();
        var (userB, _) = await SeedUserAsync();

        await SeedNotificationAsync(userA, "Unread A1", "M1", isRead: false);
        await SeedNotificationAsync(userA, "Unread A2", "M2", isRead: false);
        await SeedNotificationAsync(userA, "Read A", "M3", isRead: true);

        // User B has unread notifications
        await SeedNotificationAsync(userB, "Unread B", "M4", isRead: false);

        SetToken(tokenA);
        using var res = await _client.GetAsync("/api/notifications/unread-count");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<UnreadCountResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(2, body.UnreadCount);
    }

    [Fact]
    public async Task MarkAsRead_SetsIsRead_AndIsIdempotent()
    {
        var (user, token) = await SeedUserAsync();
        var notif = await SeedNotificationAsync(user, "Pending order", "Order submitted", isRead: false);

        SetToken(token);

        // First mark as read
        using var res1 = await _client.PutAsync($"/api/notifications/{notif.Id}/read", null);
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        var read1 = await res1.Content.ReadFromJsonAsync<NotificationResponseDto>();
        Assert.NotNull(read1);
        Assert.True(read1.IsRead);
        Assert.NotNull(read1.ReadAt);

        var firstReadAt = read1.ReadAt.Value;

        // Idempotent retry
        using var res2 = await _client.PutAsync($"/api/notifications/{notif.Id}/read", null);
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        var read2 = await res2.Content.ReadFromJsonAsync<NotificationResponseDto>();
        Assert.NotNull(read2);
        Assert.True(read2.IsRead);
        Assert.Equal(firstReadAt, read2.ReadAt);
    }

    [Fact]
    public async Task MarkAsRead_Returns404_WhenNotificationDoesNotExist()
    {
        var (_, token) = await SeedUserAsync();
        SetToken(token);

        using var res = await _client.PutAsync($"/api/notifications/{Guid.NewGuid()}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task MarkAsRead_Returns404_WhenNotificationBelongsToAnotherCustomer()
    {
        var (userA, tokenA) = await SeedUserAsync();
        var (userB, _) = await SeedUserAsync();

        var notifB = await SeedNotificationAsync(userB, "Private note", "Only for User B", isRead: false);

        // User A tries to read User B's notification
        SetToken(tokenA);
        using var res = await _client.PutAsync($"/api/notifications/{notifB.Id}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        // User B's notification remains unread
        await _factory.WithDbAsync(async db =>
        {
            var check = await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == notifB.Id);
            Assert.False(check.IsRead);
        });
    }

    [Fact]
    public async Task MarkAllAsRead_MarksAllCallerNotificationsRead_LeavesOthersUntouched()
    {
        var (userA, tokenA) = await SeedUserAsync();
        var (userB, _) = await SeedUserAsync();

        await SeedNotificationAsync(userA, "A1", "M1", isRead: false);
        await SeedNotificationAsync(userA, "A2", "M2", isRead: false);
        await SeedNotificationAsync(userA, "A3 already read", "M3", isRead: true);

        var notifB = await SeedNotificationAsync(userB, "B1", "M4", isRead: false);

        SetToken(tokenA);
        using var res = await _client.PutAsync("/api/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<ReadAllNotificationsResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(2, body.UpdatedCount);

        // Verify in DB
        await _factory.WithDbAsync(async db =>
        {
            var userANotifs = await db.Notifications.Where(n => n.UserId == userA).ToListAsync();
            Assert.All(userANotifs, n => Assert.True(n.IsRead));

            var userBNotif = await db.Notifications.SingleAsync(n => n.Id == notifB.Id);
            Assert.False(userBNotif.IsRead);
        });
    }

    [Fact]
    public async Task Anonymous_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        using var getRes = await _client.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, getRes.StatusCode);

        using var countRes = await _client.GetAsync("/api/notifications/unread-count");
        Assert.Equal(HttpStatusCode.Unauthorized, countRes.StatusCode);

        using var putRes = await _client.PutAsync($"/api/notifications/{Guid.NewGuid()}/read", null);
        Assert.Equal(HttpStatusCode.Unauthorized, putRes.StatusCode);

        using var allRes = await _client.PutAsync("/api/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.Unauthorized, allRes.StatusCode);
    }

    [Fact]
    public async Task NonCustomerRole_Returns403()
    {
        var (_, adminToken) = await SeedUserAsync(role: AppRoles.Admin);
        SetToken(adminToken);

        using var getRes = await _client.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.Forbidden, getRes.StatusCode);

        using var countRes = await _client.GetAsync("/api/notifications/unread-count");
        Assert.Equal(HttpStatusCode.Forbidden, countRes.StatusCode);
    }

    [Fact]
    public async Task InactiveCustomer_Returns403()
    {
        var (_, inactiveToken) = await SeedUserAsync(role: AppRoles.Customer, status: "INACTIVE");
        SetToken(inactiveToken);

        using var getRes = await _client.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.Forbidden, getRes.StatusCode);

        using var countRes = await _client.GetAsync("/api/notifications/unread-count");
        Assert.Equal(HttpStatusCode.Forbidden, countRes.StatusCode);
    }
}
