using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SmartDroneDelivery.Tests;

public class AuthApiTests : IClassFixture<AuthApiFactory>
{
    private const string AdminEmail = "admin@smartdronedelivery.com";
    private const string AdminPassword = "Admin@123456";
    private const string Password = "Passw0rd123";

    private static int _counter;

    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public AuthApiTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ---------- helpers ----------

    private static (string Email, string Phone) NewIdentity()
    {
        var n = Interlocked.Increment(ref _counter);
        return ($"user{n}@example.com", $"08{n:D8}");
    }

    private static object RegisterBody(string email, string phone, string password = Password) =>
        new { email, password, fullName = "Nguyen Van An", phoneNumber = phone };

    private async Task<JsonElement> RegisterAsync(string? email = null, string? phone = null)
    {
        var identity = NewIdentity();
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            RegisterBody(email ?? identity.Email, phone ?? identity.Phone));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpResponseMessage> PostAsync(string url, object body, string? bearer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await _client.SendAsync(request);
    }

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    // ---------- register ----------

    [Fact]
    public async Task Register_creates_a_customer_and_signs_in()
    {
        var auth = await RegisterAsync();

        Assert.False(string.IsNullOrEmpty(Str(auth, "accessToken")));
        Assert.False(string.IsNullOrEmpty(Str(auth, "refreshToken")));
        Assert.Equal(900, auth.GetProperty("expiresIn").GetInt32());

        var user = auth.GetProperty("user");
        Assert.Equal("CUSTOMER", user.GetProperty("role").GetProperty("code").GetString());
        Assert.Equal("ACTIVE", Str(user, "status"));
        Assert.False(user.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Register_stores_a_bcrypt_hash_not_the_password()
    {
        var (email, phone) = NewIdentity();
        await RegisterAsync(email, phone);

        await _factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            Assert.NotEqual(Password, user.PasswordHash);
            Assert.StartsWith("$2", user.PasswordHash);
        });
    }

    [Fact]
    public async Task Register_rejects_duplicate_email_and_phone_with_409()
    {
        var (email, phone) = NewIdentity();
        await RegisterAsync(email, phone);

        var sameEmail = await PostAsync("/api/auth/register", RegisterBody(email, NewIdentity().Phone));
        Assert.Equal(HttpStatusCode.Conflict, sameEmail.StatusCode);
        var body = await sameEmail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Email is already registered.", Str(body, "detail"));

        var samePhone = await PostAsync("/api/auth/register", RegisterBody(NewIdentity().Email, phone));
        Assert.Equal(HttpStatusCode.Conflict, samePhone.StatusCode);
    }

    [Fact]
    public async Task Register_treats_email_case_insensitively()
    {
        var (email, phone) = NewIdentity();
        await RegisterAsync(email, phone);

        var response = await PostAsync("/api/auth/register", RegisterBody(email.ToUpperInvariant(), NewIdentity().Phone));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "Passw0rd123", "0811111111", "email")]
    [InlineData("a@example.com", "short1", "0811111111", "password")]
    [InlineData("a@example.com", "onlyletters", "0811111111", "password")]
    [InlineData("a@example.com", "12345678", "0811111111", "password")]
    [InlineData("a@example.com", "Passw0rd123", "12345", "phoneNumber")]
    [InlineData("a@example.com", "Passw0rd123", "09abc12345", "phoneNumber")]
    public async Task Register_rejects_invalid_input_with_400_and_field_errors(
        string email, string password, string phone, string field)
    {
        var response = await PostAsync("/api/auth/register", RegisterBody(email, phone, password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.Equal("https://httpstatuses.com/400", Str(body, "type"));
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out var messages));
        Assert.True(messages.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Register_requires_all_fields()
    {
        var response = await PostAsync("/api/auth/register", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        foreach (var field in new[] { "email", "password", "fullName", "phoneNumber" })
            Assert.True(errors.TryGetProperty(field, out _), $"missing error for {field}");
    }

    // ---------- login ----------

    [Fact]
    public async Task Login_with_valid_credentials_returns_tokens()
    {
        var (email, phone) = NewIdentity();
        await RegisterAsync(email, phone);

        var response = await PostAsync("/api/auth/login", new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(Str(auth, "accessToken")));
        Assert.False(string.IsNullOrEmpty(Str(auth, "refreshToken")));
        Assert.Equal(email, Str(auth.GetProperty("user"), "email"));
    }

    [Fact]
    public async Task Login_access_token_carries_sub_email_and_role_and_lasts_15_minutes()
    {
        var response = await PostAsync("/api/auth/login", new { email = AdminEmail, password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(Str(auth, "accessToken"));
        Assert.Equal(Str(auth.GetProperty("user"), "id"), jwt.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal(AdminEmail, jwt.Claims.First(c => c.Type == "email").Value);
        Assert.Equal("ADMIN", jwt.Claims.First(c => c.Type == "role").Value);
        Assert.Equal(TimeSpan.FromMinutes(15), jwt.ValidTo - jwt.ValidFrom);
    }

    [Fact]
    public async Task Login_is_case_insensitive_on_email()
    {
        var response = await PostAsync("/api/auth/login",
            new { email = AdminEmail.ToUpperInvariant(), password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_wrong_password_and_unknown_email_give_the_same_401()
    {
        var (email, phone) = NewIdentity();
        await RegisterAsync(email, phone);

        var wrongPassword = await PostAsync("/api/auth/login", new { email, password = "Wrong-pass1" });
        var unknownEmail = await PostAsync("/api/auth/login", new { email = "nobody@example.com", password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var a = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
        var b = await unknownEmail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Str(a, "detail"), Str(b, "detail"));
    }

    [Fact]
    public async Task Login_requires_email_and_password()
    {
        var response = await PostAsync("/api/auth/login", new { email = "", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("INACTIVE")]
    [InlineData("SUSPENDED")]
    public async Task Login_inactive_or_suspended_account_gets_403(string status)
    {
        var (email, phone) = NewIdentity();
        await RegisterAsync(email, phone);
        await _factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Email == email)).Status = status;
            await db.SaveChangesAsync();
        });

        var response = await PostAsync("/api/auth/login", new { email, password = Password });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("This account is not active.", Str(body, "detail"));
    }

    [Fact]
    public async Task Login_writes_an_audit_log_entry()
    {
        var (email, phone) = NewIdentity();
        var auth = await RegisterAsync(email, phone);
        await PostAsync("/api/auth/login", new { email, password = Password });

        var userId = Guid.Parse(Str(auth.GetProperty("user"), "id"));
        await _factory.WithDbAsync(async db =>
            Assert.True(await db.AuditLogs.AnyAsync(a => a.UserId == userId && a.Action == "LOGIN")));
    }

    // ---------- refresh ----------

    [Fact]
    public async Task Refresh_rotates_the_token_and_revokes_the_old_one()
    {
        var auth = await RegisterAsync();
        var oldRefresh = Str(auth, "refreshToken");

        var response = await PostAsync("/api/auth/refresh", new { refreshToken = oldRefresh });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renewed = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(oldRefresh, Str(renewed, "refreshToken"));
        Assert.NotEqual(Str(auth, "accessToken"), Str(renewed, "accessToken"));

        await _factory.WithDbAsync(async db =>
            Assert.True((await db.RefreshTokens.SingleAsync(t => t.Token == oldRefresh)).IsRevoked));
    }

    [Fact]
    public async Task Refresh_old_token_cannot_be_used_twice()
    {
        var auth = await RegisterAsync();
        var oldRefresh = Str(auth, "refreshToken");
        await PostAsync("/api/auth/refresh", new { refreshToken = oldRefresh });

        var replay = await PostAsync("/api/auth/refresh", new { refreshToken = oldRefresh });

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Refresh_new_token_works()
    {
        var auth = await RegisterAsync();
        var first = await (await PostAsync("/api/auth/refresh", new { refreshToken = Str(auth, "refreshToken") }))
            .Content.ReadFromJsonAsync<JsonElement>();

        var second = await PostAsync("/api/auth/refresh", new { refreshToken = Str(first, "refreshToken") });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Refresh_unknown_token_gets_401_and_missing_token_gets_400()
    {
        var unknown = await PostAsync("/api/auth/refresh", new { refreshToken = "does-not-exist" });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);

        var missing = await PostAsync("/api/auth/refresh", new { refreshToken = "" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task Refresh_expired_token_gets_401()
    {
        var auth = await RegisterAsync();
        var token = Str(auth, "refreshToken");
        await _factory.WithDbAsync(async db =>
        {
            (await db.RefreshTokens.SingleAsync(t => t.Token == token)).ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        var response = await PostAsync("/api/auth/refresh", new { refreshToken = token });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_token_lives_7_days()
    {
        var auth = await RegisterAsync();
        var token = Str(auth, "refreshToken");

        await _factory.WithDbAsync(async db =>
        {
            var row = await db.RefreshTokens.SingleAsync(t => t.Token == token);
            var lifetime = row.ExpiresAt - row.CreatedAt;
            Assert.Equal(TimeSpan.FromDays(7), lifetime);
        });
    }

    [Fact]
    public async Task Refresh_for_a_user_that_was_deactivated_gets_401()
    {
        var (email, phone) = NewIdentity();
        var auth = await RegisterAsync(email, phone);
        await _factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Email == email)).Status = "SUSPENDED";
            await db.SaveChangesAsync();
        });

        var response = await PostAsync("/api/auth/refresh", new { refreshToken = Str(auth, "refreshToken") });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- logout ----------

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var auth = await RegisterAsync();
        var refresh = Str(auth, "refreshToken");

        var logout = await PostAsync("/api/auth/logout", new { refreshToken = refresh }, Str(auth, "accessToken"));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refreshAfter = await PostAsync("/api/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfter.StatusCode);
    }

    [Fact]
    public async Task Logout_is_safe_to_repeat_and_ignores_unknown_tokens()
    {
        var auth = await RegisterAsync();
        var bearer = Str(auth, "accessToken");
        var refresh = Str(auth, "refreshToken");

        Assert.Equal(HttpStatusCode.NoContent,
            (await PostAsync("/api/auth/logout", new { refreshToken = refresh }, bearer)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await PostAsync("/api/auth/logout", new { refreshToken = refresh }, bearer)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await PostAsync("/api/auth/logout", new { refreshToken = "does-not-exist" }, bearer)).StatusCode);
    }

    [Fact]
    public async Task Logout_requires_a_valid_access_token()
    {
        var auth = await RegisterAsync();

        var noToken = await PostAsync("/api/auth/logout", new { refreshToken = Str(auth, "refreshToken") });
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);
        var body = await noToken.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(401, body.GetProperty("status").GetInt32());

        var badToken = await PostAsync("/api/auth/logout", new { refreshToken = "x" }, "not.a.jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, badToken.StatusCode);
    }

    [Fact]
    public async Task Logout_cannot_revoke_a_token_that_belongs_to_another_user()
    {
        var alice = await RegisterAsync();
        var bob = await RegisterAsync();

        var response = await PostAsync("/api/auth/logout",
            new { refreshToken = Str(alice, "refreshToken") }, Str(bob, "accessToken"));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var aliceRefresh = await PostAsync("/api/auth/refresh", new { refreshToken = Str(alice, "refreshToken") });
        Assert.Equal(HttpStatusCode.OK, aliceRefresh.StatusCode);
    }

    [Fact]
    public async Task Logout_writes_an_audit_log_entry()
    {
        var auth = await RegisterAsync();
        await PostAsync("/api/auth/logout", new { refreshToken = Str(auth, "refreshToken") }, Str(auth, "accessToken"));

        var userId = Guid.Parse(Str(auth.GetProperty("user"), "id"));
        await _factory.WithDbAsync(async db =>
            Assert.True(await db.AuditLogs.AnyAsync(a => a.UserId == userId && a.Action == "LOGOUT")));
    }
}
