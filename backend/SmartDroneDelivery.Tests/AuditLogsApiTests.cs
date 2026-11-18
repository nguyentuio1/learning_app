using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>LA-177: failed sign-ins are audited; administrators search the audit log (FR-33, F-19).</summary>
public class AuditLogsApiTests : IClassFixture<AuthApiFactory>, IDisposable
{
    private const string Password = "Passw0rd123";
    private static readonly string PasswordHash = new PasswordHasher().Hash(Password);
    private static int _counter;
    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public AuditLogsApiTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private async Task<(Guid Id, string Email)> SeedUserAsync(string role, string status = "ACTIVE")
    {
        var n = Interlocked.Increment(ref _counter);
        var user = new User
        {
            Id = Guid.NewGuid(), Email = $"audit{n}-{Guid.NewGuid():N}@example.com", FullName = $"Audit User {n}",
            PhoneNumber = $"06{n:D8}", PasswordHash = PasswordHash, Status = status
        };
        await _factory.WithDbAsync(async db =>
        {
            user.RoleId = (await db.Roles.SingleAsync(r => r.Code == role)).Id;
            db.Users.Add(user);
            await db.SaveChangesAsync();
        });
        return (user.Id, user.Email);
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private async Task<string> TokenAsync(string email, string password = Password)
    {
        using var response = await LoginAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private Task<string> AdminTokenAsync() => TokenAsync(TestSettings.AdminEmail, TestSettings.AdminPassword);

    private async Task<HttpResponseMessage> GetAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<JsonElement> SearchAsync(string query, string token)
    {
        using var response = await GetAsync($"/api/audit-logs{query}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task FailLoginAsync(string email, string password)
    {
        using var response = await LoginAsync(email, password);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_for_an_existing_account_writes_LOGIN_FAILED_without_the_password()
    {
        var (id, email) = await SeedUserAsync("CUSTOMER");

        await FailLoginAsync(email, "Wrong-Password-9");

        await _factory.WithDbAsync(async db =>
        {
            var row = await db.AuditLogs.SingleAsync(a => a.UserId == id);
            Assert.Equal("LOGIN_FAILED", row.Action);
            Assert.Equal("INVALID_PASSWORD", row.Reason);
            Assert.Equal(id.ToString(), row.EntityId);
            Assert.DoesNotContain("Wrong-Password-9", $"{row.Reason}{row.EntityId}{row.UserAgent}");
        });
    }

    [Fact]
    public async Task Inactive_account_sign_in_is_audited_and_unknown_email_is_not_stored()
    {
        var (id, email) = await SeedUserAsync("DISPATCHER", status: "INACTIVE");
        var before = 0;
        await _factory.WithDbAsync(async db => before = await db.AuditLogs.CountAsync());

        using (var inactive = await LoginAsync(email, Password))
            Assert.Equal(HttpStatusCode.Forbidden, inactive.StatusCode);
        await FailLoginAsync($"nobody-{Guid.NewGuid():N}@example.com", Password);

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(before + 1, await db.AuditLogs.CountAsync());
            var row = await db.AuditLogs.SingleAsync(a => a.UserId == id);
            Assert.Equal(("LOGIN_FAILED", "ACCOUNT_NOT_ACTIVE"), (row.Action, row.Reason));
        });
    }

    [Fact]
    public async Task Admin_searches_by_user_action_and_time_newest_first_with_actor_details()
    {
        var admin = await AdminTokenAsync();
        var (id, email) = await SeedUserAsync("CUSTOMER");
        await FailLoginAsync(email, "Wrong-Password-1");
        await TokenAsync(email);
        await FailLoginAsync(email, "Wrong-Password-2");

        var byUser = await SearchAsync($"?userId={id}", admin);
        Assert.Equal(3, byUser.GetProperty("totalCount").GetInt32());
        var actions = byUser.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("action").GetString()).ToArray();
        Assert.Equal(new[] { "LOGIN_FAILED", "LOGIN", "LOGIN_FAILED" }, actions);
        var first = byUser.GetProperty("items")[0];
        Assert.Equal(email, first.GetProperty("userEmail").GetString());
        Assert.EndsWith("Z", first.GetProperty("timestamp").GetString());
        Assert.False(first.TryGetProperty("passwordHash", out _));

        var failed = await SearchAsync($"?userId={id}&action=login_failed", admin);
        Assert.Equal(2, failed.GetProperty("totalCount").GetInt32());

        var later = Uri.EscapeDataString(DateTime.UtcNow.AddHours(1).ToString("O"));
        var future = await SearchAsync($"?userId={id}&from={later}", admin);
        Assert.Equal(0, future.GetProperty("totalCount").GetInt32());
        Assert.Empty(future.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Pages_are_bounded_and_ordered()
    {
        var admin = await AdminTokenAsync();
        var (id, email) = await SeedUserAsync("CUSTOMER");
        for (var i = 0; i < 5; i++) await FailLoginAsync(email, $"Wrong-{i}-Password");

        var page2 = await SearchAsync($"?userId={id}&page=2&pageSize=2", admin);
        var all = await SearchAsync($"?userId={id}&pageSize=5", admin);

        Assert.Equal(5, page2.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, page2.GetProperty("totalPages").GetInt32());
        var ids = all.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt64()).ToArray();
        Assert.Equal(ids.OrderByDescending(x => x).ToArray(), ids);
        Assert.Equal(ids[2..4], page2.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt64()).ToArray());
    }

    [Theory]
    [InlineData("?pageSize=101", "pageSize")]
    [InlineData("?page=0", "page")]
    [InlineData("?userId=not-a-guid", "userId")]
    [InlineData("?from=yesterday", "from")]
    [InlineData("?from=2026-11-17&to=2026-11-16", "to")]
    [InlineData("?sort=asc", "sort")]
    [InlineData("?action=", "action")]
    public async Task Invalid_query_returns_400_naming_the_parameter(string query, string field)
    {
        var admin = await AdminTokenAsync();

        using var response = await GetAsync($"/api/audit-logs{query}", admin);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("CUSTOMER")]
    [InlineData("DISPATCHER")]
    [InlineData("MANAGER")]
    [InlineData("STATION_OPERATOR")]
    public async Task Only_administrators_can_read_the_audit_log(string role)
    {
        var (_, email) = await SeedUserAsync(role);
        var token = await TokenAsync(email);

        using var search = await GetAsync("/api/audit-logs", token);
        using var actions = await GetAsync("/api/audit-logs/actions", token);
        using var anonymous = await _client.GetAsync("/api/audit-logs");

        Assert.Equal(HttpStatusCode.Forbidden, search.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, actions.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Actions_lists_distinct_codes_for_the_filter()
    {
        var admin = await AdminTokenAsync();
        var (_, email) = await SeedUserAsync("CUSTOMER");
        await FailLoginAsync(email, "Wrong-Password-3");

        using var response = await GetAsync("/api/audit-logs/actions", admin);
        var codes = await response.Content.ReadFromJsonAsync<string[]>();

        Assert.Contains("LOGIN", codes!);
        Assert.Contains("LOGIN_FAILED", codes!);
        Assert.Equal(codes!.Distinct().Order(StringComparer.Ordinal).ToArray(), codes);
    }
}
