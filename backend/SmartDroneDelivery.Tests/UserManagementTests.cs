using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>LA-92: real HTTP endpoints, middleware, EF persistence and audit records.</summary>
public class UserManagementTests : IClassFixture<AuthApiFactory>, IDisposable
{
    private const string Password = "Passw0rd123";
    private static readonly string PasswordHash = new PasswordHasher().Hash(Password);
    private static int _counter;
    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public UserManagementTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private static object Details(string? email = null, string? phone = null, string role = "CUSTOMER")
    {
        var n = Interlocked.Increment(ref _counter);
        return new
        {
            email = email ?? $"managed{n}@example.com", password = Password,
            fullName = "Managed User", phoneNumber = phone ?? $"08{n:D8}", role
        };
    }

    private async Task<JsonElement> SignInAsync(string email, string password = Password)
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<string> AdminTokenAsync() =>
        (await SignInAsync("admin@smartdronedelivery.com", "Admin@123456"))
        .GetProperty("accessToken").GetString()!;

    private async Task<JsonElement> SeedSessionAsync(string role)
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"actor{n}@example.com";
        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(new User
            {
                Email = email, FullName = "HTTP Test Actor", PhoneNumber = $"07{n:D8}",
                PasswordHash = PasswordHash,
                RoleId = (await db.Roles.SingleAsync(r => r.Code == role)).Id
            });
            await db.SaveChangesAsync();
        });
        return await SignInAsync(email);
    }

    private async Task<HttpResponseMessage> SendAsync(
        string method, string path, string? token = null, object? body = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("LA92-HttpTests/1.0");
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private async Task<JsonElement> CreateAsync(string token, object? body = null)
    {
        using var response = await SendAsync("POST", "/api/users", token, body ?? Details());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.EndsWith($"/api/users/{user.GetProperty("id").GetString()}", response.Headers.Location!.ToString());
        return user;
    }

    public static IEnumerable<object[]> Endpoints()
    {
        const string id = "11111111-1111-1111-1111-111111111111";
        yield return new object[] { "GET", "/api/users" };
        yield return new object[] { "GET", $"/api/users/{id}" };
        yield return new object[] { "POST", "/api/users" };
        yield return new object[] { "PUT", $"/api/users/{id}" };
        yield return new object[] { "PUT", $"/api/users/{id}/role" };
        yield return new object[] { "PUT", $"/api/users/{id}/deactivate" };
    }

    public static IEnumerable<object[]> ForbiddenRoles() =>
        from role in new[] { AppRoles.Customer, AppRoles.Dispatcher, AppRoles.StationOperator, AppRoles.Manager }
        from endpoint in Endpoints()
        select new[] { role, endpoint[0], endpoint[1] };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Every_admin_endpoint_returns_401_without_a_token(string method, string path)
    {
        using var response = await SendAsync(method, path, body: Details());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(401, error.GetProperty("status").GetInt32());
    }

    [Theory]
    [MemberData(nameof(ForbiddenRoles))]
    public async Task Other_roles_get_403_for_every_admin_endpoint(string role, string method, string path)
    {
        var session = await SeedSessionAsync(role);
        using var response = await SendAsync(method, path, session.GetProperty("accessToken").GetString(), Details());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(403, error.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Admin_can_create_list_read_update_assign_role_and_deactivate_with_audits()
    {
        var token = await AdminTokenAsync();
        var user = await CreateAsync(token);
        var id = user.GetProperty("id").GetGuid();
        var url = $"/api/users/{id}";
        using var list = await SendAsync("GET", "/api/users", token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains((await list.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == id);
        using var get = await SendAsync("GET", url, token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.DoesNotContain("password", await get.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var email = user.GetProperty("email").GetString()!;
        var phone = user.GetProperty("phoneNumber").GetString()!;
        using var update = await SendAsync("PUT", url, token,
            new { email = email.ToUpperInvariant(), fullName = " Updated Name ", phoneNumber = phone,
                role = "ADMIN", status = "INACTIVE", password = "Ignored123" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Name", updated.GetProperty("fullName").GetString());
        Assert.Equal(email, updated.GetProperty("email").GetString());
        Assert.Equal("CUSTOMER", updated.GetProperty("roleCode").GetString());
        Assert.Equal("ACTIVE", updated.GetProperty("status").GetString());
        using var role = await SendAsync("PUT", url + "/role", token, new { role = "DISPATCHER" });
        Assert.Equal(HttpStatusCode.NoContent, role.StatusCode);
        using var detail = await SendAsync("GET", url, token);
        Assert.Equal("DISPATCHER",
            (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("roleCode").GetString());

        var firstSession = await SignInAsync(email);
        var secondSession = await SignInAsync(email);
        using var deactivate = await SendAsync("PUT", url + "/deactivate", token);
        using var retry = await SendAsync("PUT", url + "/deactivate", token);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        using var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        foreach (var session in new[] { firstSession, secondSession })
        {
            using var refresh = await _client.PostAsJsonAsync("/api/auth/refresh",
                new { refreshToken = session.GetProperty("refreshToken").GetString() });
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
        await _factory.WithDbAsync(async db =>
        {
            var stored = await db.Users.Include(u => u.Role).SingleAsync(u => u.Id == id);
            Assert.Equal("INACTIVE", stored.Status);
            Assert.Equal("Updated Name", stored.FullName);
            Assert.Equal("DISPATCHER", stored.Role.Code);
            Assert.NotEqual(Password, stored.PasswordHash);
            Assert.True(new PasswordHasher().Verify(Password, stored.PasswordHash));
            Assert.All(await db.RefreshTokens.Where(t => t.UserId == id).ToListAsync(), t => Assert.True(t.IsRevoked));
            var audits = await db.AuditLogs.Where(a => a.EntityId == id.ToString() &&
                new[] { "CREATE_USER", "USER_UPDATE", "ROLE_CHANGE", "USER_DEACTIVATE" }.Contains(a.Action)).ToListAsync();
            Assert.Equal(4, audits.Count);
            Assert.Equal(new[] { "CREATE_USER", "ROLE_CHANGE", "USER_DEACTIVATE", "USER_UPDATE" },
                audits.Select(a => a.Action).OrderBy(a => a));
            var actorId = (await db.Users.SingleAsync(u => u.Email == "admin@smartdronedelivery.com")).Id;
            Assert.All(audits, a =>
            {
                Assert.Equal(actorId, a.UserId);
                Assert.Equal("User", a.EntityName);
                Assert.Equal("LA92-HttpTests/1.0", a.UserAgent);
            });
        });
    }

    [Theory]
    [InlineData("POST", "email", "Email is already registered.")]
    [InlineData("POST", "phone", "Phone number is already registered.")]
    [InlineData("PUT", "email", "Email is already registered.")]
    [InlineData("PUT", "phone", "Phone number is already registered.")]
    public async Task Duplicate_fields_return_the_exact_409_contract_without_saving(
        string method, string field, string message)
    {
        var token = await AdminTokenAsync();
        var existing = await CreateAsync(token);
        var target = await CreateAsync(token);
        var id = target.GetProperty("id").GetGuid();
        var body = Details(
            field == "email" ? existing.GetProperty("email").GetString()!.ToUpperInvariant() : null,
            field == "phone" ? existing.GetProperty("phoneNumber").GetString() : null);
        using var response = await SendAsync(method, method == "POST" ? "/api/users" : $"/api/users/{id}", token, body);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(message, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        await _factory.WithDbAsync(async db =>
        {
            var stored = await db.Users.SingleAsync(u => u.Id == id);
            Assert.Equal(target.GetProperty("email").GetString(), stored.Email);
            Assert.Equal(target.GetProperty("phoneNumber").GetString(), stored.PhoneNumber);
            Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityId == id.ToString() && a.Action == "USER_UPDATE"));
        });
    }

    [Theory]
    [InlineData("email", "not-an-email")]
    [InlineData("password", "short")]
    [InlineData("password", "lettersOnly")]
    [InlineData("fullName", "   ")]
    [InlineData("phoneNumber", "abc")]
    [InlineData("role", "UNKNOWN")]
    public async Task Invalid_create_body_returns_field_errors_and_does_not_create_a_user(string field, string value)
    {
        var token = await AdminTokenAsync();
        var body = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(Details()))!;
        body[field] = value;
        using var response = await SendAsync("POST", "/api/users", token, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(error.GetProperty("errors").TryGetProperty(field, out _));
        var email = body["email"].ToString();
        await _factory.WithDbAsync(async db => Assert.False(await db.Users.AnyAsync(u => u.Email == email)));
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("PUT", "")]
    [InlineData("PUT", "/role")]
    [InlineData("PUT", "/deactivate")]
    public async Task Unknown_target_returns_404(string method, string suffix)
    {
        using var response = await SendAsync(method, $"/api/users/{Guid.NewGuid()}{suffix}", await AdminTokenAsync(), Details());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Noop_updates_do_not_create_duplicate_audits()
    {
        var token = await AdminTokenAsync();
        var user = await CreateAsync(token);
        var id = user.GetProperty("id").GetGuid();
        using var update = await SendAsync("PUT", $"/api/users/{id}", token, new
        {
            email = user.GetProperty("email").GetString(), fullName = user.GetProperty("fullName").GetString(),
            phoneNumber = user.GetProperty("phoneNumber").GetString()
        });
        using var role = await SendAsync("PUT", $"/api/users/{id}/role", token, new { role = "CUSTOMER" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, role.StatusCode);
        await _factory.WithDbAsync(async db =>
            Assert.Single(await db.AuditLogs.Where(a => a.EntityId == id.ToString()).ToListAsync()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admin_cannot_disable_or_demote_self_even_when_the_last_admin(bool anotherAdmin)
    {
        // Dedicated database so the "last admin" case is independent of other tests.
        using var isolatedFactory = new AuthApiFactory();
        using var isolated = new UserManagementTests(isolatedFactory);
        if (anotherAdmin) await isolated.SeedSessionAsync("ADMIN");
        var token = await isolated.AdminTokenAsync();
        Guid id = default;
        await isolatedFactory.WithDbAsync(async db =>
            id = (await db.Users.SingleAsync(u => u.Email == "admin@smartdronedelivery.com")).Id);
        using var deactivate = await isolated.SendAsync("PUT", $"/api/users/{id}/deactivate", token);
        using var demote = await isolated.SendAsync("PUT", $"/api/users/{id}/role", token, new { role = "CUSTOMER" });
        using var noop = await isolated.SendAsync("PUT", $"/api/users/{id}/role", token, new { role = "ADMIN" });
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, noop.StatusCode);
        await isolatedFactory.WithDbAsync(async db =>
        {
            var admin = await db.Users.Include(u => u.Role).SingleAsync(u => u.Id == id);
            Assert.Equal("ACTIVE", admin.Status);
            Assert.Equal("ADMIN", admin.Role.Code);
            Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityId == id.ToString() &&
                (a.Action == "ROLE_CHANGE" || a.Action == "USER_DEACTIVATE")));
        });
    }

    [Theory]
    [InlineData("deactivate")]
    [InlineData("role")]
    public async Task Changed_admin_cannot_reuse_old_admin_token(string action)
    {
        var session = await SeedSessionAsync("ADMIN");
        var id = session.GetProperty("user").GetProperty("id").GetGuid();
        using var change = await SendAsync("PUT", $"/api/users/{id}/{action}", await AdminTokenAsync(), new { role = "CUSTOMER" });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        using var denied = await SendAsync("GET", "/api/users", session.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
