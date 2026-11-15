using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>
/// LA-326: Concurrent notification read/read-all requests must be atomic on a relational DB,
/// preserve the first ReadAt timestamp, and return accurate affected-row counts.
/// </summary>
public class NotificationConcurrencyTests : IClassFixture<PostgresApiFactory>, IDisposable
{
    private const string Password = "Passw0rd123";
    private static readonly string PasswordHash = new PasswordHasher().Hash(Password);
    private static int _counter;

    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _client;

    public NotificationConcurrencyTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _client = PostgresApiFactory.ServerConnectionString is null ? new HttpClient() : factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    [PostgresFact]
    public async Task Concurrent_mark_as_read_preserves_first_read_at()
    {
        var (customerId, token) = await SeedCustomerAsync();
        var notificationId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            db.Notifications.Add(new Notification
            {
                Id = notificationId,
                UserId = customerId,
                Type = "ORDER_CREATED",
                Title = "Order created",
                Message = "Your order was created.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await db.SaveChangesAsync();
        });

        const int simultaneous = 6;
        var responses = await FireTogetherAsync(
            Enumerable.Range(0, simultaneous)
                .Select(_ => (Func<Task<HttpResponseMessage>>)(() => PatchReadAsync(notificationId, token)))
                .ToArray());

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var dtos = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<NotificationResponseDto>()));
        Assert.All(dtos, dto =>
        {
            Assert.NotNull(dto);
            Assert.True(dto!.IsRead);
            Assert.NotNull(dto.ReadAt);
        });

        DateTime? persistedReadAt = null;
        await _factory.WithDbAsync(async db =>
        {
            persistedReadAt = (await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == notificationId)).ReadAt;
        });

        Assert.NotNull(persistedReadAt);
        Assert.All(dtos, dto => Assert.Equal(
            DateTime.SpecifyKind(persistedReadAt!.Value, DateTimeKind.Utc),
            dto!.ReadAt!.Value,
            TimeSpan.FromMilliseconds(1)));
    }

    [PostgresFact]
    public async Task Concurrent_mark_all_as_read_counts_each_unread_row_at_most_once()
    {
        var (customerId, token) = await SeedCustomerAsync();
        const int unreadCount = 5;

        await _factory.WithDbAsync(async db =>
        {
            for (var i = 0; i < unreadCount; i++)
            {
                db.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = customerId,
                    Type = "ORDER_STATUS_CHANGED",
                    Title = $"Update {i}",
                    Message = $"Message {i}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-i)
                });
            }
            await db.SaveChangesAsync();
        });

        const int simultaneous = 4;
        var responses = await FireTogetherAsync(
            Enumerable.Range(0, simultaneous)
                .Select(_ => (Func<Task<HttpResponseMessage>>)(() => PostReadAllAsync(token)))
                .ToArray());

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var dtos = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<ReadAllNotificationsResponseDto>()));
        Assert.Equal(unreadCount, dtos.Sum(d => d!.UpdatedCount));

        await _factory.WithDbAsync(async db =>
        {
            var rows = await db.Notifications.AsNoTracking().Where(n => n.UserId == customerId).ToListAsync();
            Assert.Equal(unreadCount, rows.Count);
            Assert.All(rows, row =>
            {
                Assert.True(row.IsRead);
                Assert.NotNull(row.ReadAt);
            });
        });
    }

    private async Task<(Guid UserId, string Token)> SeedCustomerAsync()
    {
        var n = Interlocked.Increment(ref _counter);
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N")[..8];
        var email = $"notifconc_{n}_{suffix}@example.com";
        var phone = $"03{Math.Abs(BitConverter.ToInt32(userId.ToByteArray(), 0)) % 100000000:D8}";

        await _factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.SingleAsync(r => r.Code == AppRoles.Customer);
            db.Users.Add(new User
            {
                Id = userId,
                Email = email,
                FullName = $"Notif Conc {n}",
                PhoneNumber = phone,
                PasswordHash = PasswordHash,
                RoleId = role.Id,
                Status = "ACTIVE"
            });
            await db.SaveChangesAsync();
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.Include(u => u.Role).SingleAsync(u => u.Id == userId);
        var token = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, DateTime.UtcNow);
        return (userId, token);
    }

    private Task<HttpResponseMessage> PatchReadAsync(Guid id, string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/notifications/{id}/read");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req);
    }

    private Task<HttpResponseMessage> PostReadAllAsync(string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Put, "/api/notifications/read-all");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage[]> FireTogetherAsync(params Func<Task<HttpResponseMessage>>[] actions)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = actions.Select(async action =>
        {
            await gate.Task;
            return await action();
        }).ToArray();
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }
}
