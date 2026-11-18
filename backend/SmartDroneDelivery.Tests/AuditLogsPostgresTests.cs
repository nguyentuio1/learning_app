using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Tests;

/// <summary>LA-177: the audit search translates to SQL on real PostgreSQL (timestamptz filters, join, paging).</summary>
public class AuditLogsPostgresTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [PostgresFact]
    public async Task Search_filters_by_user_action_and_time_range_on_postgres()
    {
        var userId = Guid.Empty;
        var t0 = new DateTime(2026, 11, 10, 1, 0, 0, DateTimeKind.Utc);
        await factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.SingleAsync(r => r.Code == "CUSTOMER");
            var user = new User
            {
                Id = Guid.NewGuid(), RoleId = role.Id, Email = $"pg-audit-{Guid.NewGuid():N}@example.com",
                FullName = "PG Audit", PhoneNumber = "0611111111", PasswordHash = "x"
            };
            userId = user.Id;
            db.Users.Add(user);
            for (var i = 0; i < 4; i++)
                db.AuditLogs.Add(new AuditLog
                {
                    UserId = user.Id, Action = i % 2 == 0 ? "LOGIN" : "LOGIN_FAILED", EntityName = "User",
                    EntityId = user.Id.ToString(), Timestamp = t0.AddHours(i)
                });
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email = TestSettings.AdminEmail, password = TestSettings.AdminPassword });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 02:00 to 03:00 UTC, given as UTC+07:00 offsets: hours 1 and 2 match.
        using var response = await client.GetAsync(
            $"/api/audit-logs?userId={userId}&from={Uri.EscapeDataString("2026-11-10T09:00:00+07:00")}&to={Uri.EscapeDataString("2026-11-10T10:00:00+07:00")}&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("totalPages").GetInt32());
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal("LOGIN", item.GetProperty("action").GetString());
        Assert.Equal(t0.AddHours(2), item.GetProperty("timestamp").GetDateTime().ToUniversalTime());
        Assert.Equal("PG Audit", item.GetProperty("userFullName").GetString());

        using var failed = await client.GetAsync($"/api/audit-logs?userId={userId}&action=LOGIN_FAILED");
        Assert.Equal(2, (await failed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
    }
}
