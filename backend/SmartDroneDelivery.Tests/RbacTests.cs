using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class RbacTests : IClassFixture<AuthApiFactory>
{
    private const string Password = "Passw0rd123";
    private static int _counter;

    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;
    private readonly Dictionary<string, string> _tokens = new();

    public RbacTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ---------- helpers ----------

    /// <summary>Creates a user with the role directly in the database and signs in through the real login endpoint.</summary>
    private async Task<string> TokenForAsync(string roleCode)
    {
        if (_tokens.TryGetValue(roleCode, out var cached))
            return cached;

        var n = Interlocked.Increment(ref _counter);
        var email = $"rbac{n}@example.com";
        await _factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.SingleAsync(r => r.Code == roleCode);
            db.Users.Add(new User
            {
                RoleId = role.Id,
                Email = email,
                PasswordHash = new PasswordHasher().Hash(Password),
                FullName = $"Test {roleCode}",
                PhoneNumber = $"07{n:D8}"
            });
            await db.SaveChangesAsync();
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
        return _tokens[roleCode] = auth.GetProperty("accessToken").GetString()!;
    }

    private async Task<HttpResponseMessage> GetAsync(string url, string? bearer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await _client.SendAsync(request);
    }

    private string SignedToken(
        string role, DateTime? expires = null, string? key = null, string? issuer = null, string? audience = null)
    {
        var jwt = _factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? jwt.Key)), SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer ?? jwt.Issuer,
            audience ?? jwt.Audience,
            new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("email", "forged@example.com"),
                new Claim("role", role)
            },
            notBefore: (expires ?? now.AddMinutes(15)).AddMinutes(-30),
            expires: expires ?? now.AddMinutes(15),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ---------- 401: no valid token ----------

    [Theory]
    [InlineData("/probe/signed-in")]
    [InlineData("/probe/no-attribute")]
    [InlineData("/probe/customer")]
    [InlineData("/probe/admin")]
    public async Task Request_without_a_token_gets_401_with_a_problem_body(string url)
    {
        var response = await GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(401, body.GetProperty("status").GetInt32());
        Assert.Equal("https://httpstatuses.com/401", body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Anonymous_endpoints_stay_open()
    {
        Assert.Equal(HttpStatusCode.OK, (await GetAsync("/probe/open")).StatusCode);
    }

    [Fact]
    public async Task Endpoint_without_any_attribute_is_protected_by_default()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/probe/no-attribute")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync("/probe/no-attribute", await TokenForAsync(AppRoles.Customer))).StatusCode);
    }

    [Fact]
    public async Task Garbage_token_gets_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/probe/signed-in", "not.a.jwt")).StatusCode);
    }

    [Fact]
    public async Task Expired_token_gets_401()
    {
        var token = SignedToken(AppRoles.Admin, expires: DateTime.UtcNow.AddMinutes(-10));
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/probe/admin", token)).StatusCode);
    }

    [Fact]
    public async Task Token_signed_with_another_key_gets_401()
    {
        var token = SignedToken(AppRoles.Admin, key: "A_Completely_Different_Signing_Key_Of_Enough_Length_123456");
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/probe/admin", token)).StatusCode);
    }

    [Fact]
    public async Task Token_with_wrong_issuer_or_audience_gets_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await GetAsync("/probe/signed-in", SignedToken(AppRoles.Admin, issuer: "someone-else"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await GetAsync("/probe/signed-in", SignedToken(AppRoles.Admin, audience: "other-clients"))).StatusCode);
    }

    // ---------- 403: valid token, wrong role ----------

    [Fact]
    public async Task Wrong_role_gets_403_with_the_contract_body()
    {
        var response = await GetAsync("/probe/admin", await TokenForAsync(AppRoles.Customer));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(403, body.GetProperty("status").GetInt32());
        Assert.Equal("Forbidden", body.GetProperty("title").GetString());
        Assert.Equal("https://httpstatuses.com/403", body.GetProperty("type").GetString());
        Assert.Equal("You do not have permission to perform this action.", body.GetProperty("detail").GetString());
    }

    public static IEnumerable<object[]> RoleMatrix()
    {
        var endpoints = new Dictionary<string, string>
        {
            [AppRoles.Customer] = "/probe/customer",
            [AppRoles.Dispatcher] = "/probe/dispatcher",
            [AppRoles.StationOperator] = "/probe/station-operator",
            [AppRoles.Manager] = "/probe/manager",
            [AppRoles.Admin] = "/probe/admin"
        };
        foreach (var role in AppRoles.All)
            foreach (var (ownerRole, url) in endpoints)
                yield return new object[] { role, url, role == ownerRole };
    }

    [Theory]
    [MemberData(nameof(RoleMatrix))]
    public async Task Each_role_can_only_call_its_own_endpoint(string role, string url, bool allowed)
    {
        var response = await GetAsync(url, await TokenForAsync(role));

        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(AppRoles.Dispatcher, HttpStatusCode.OK)]
    [InlineData(AppRoles.Admin, HttpStatusCode.OK)]
    [InlineData(AppRoles.Customer, HttpStatusCode.Forbidden)]
    [InlineData(AppRoles.Manager, HttpStatusCode.Forbidden)]
    public async Task Endpoint_can_allow_several_roles(string role, HttpStatusCode expected)
    {
        var response = await GetAsync("/probe/dispatcher-or-admin", await TokenForAsync(role));
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Every_role_can_call_an_endpoint_that_only_needs_a_signed_in_user()
    {
        foreach (var role in AppRoles.All)
            Assert.Equal(HttpStatusCode.OK, (await GetAsync("/probe/signed-in", await TokenForAsync(role))).StatusCode);
    }

    [Fact]
    public async Task Unknown_role_in_a_valid_token_gets_403()
    {
        var token = SignedToken("HACKER");
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync("/probe/customer", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync("/probe/admin", token)).StatusCode);
    }

    // ---------- consistency with the database ----------

    [Fact]
    public async Task App_role_codes_match_the_seeded_roles()
    {
        await _factory.WithDbAsync(async db =>
        {
            var codes = await db.Roles.Select(r => r.Code).ToListAsync();
            Assert.Equal(AppRoles.All.OrderBy(x => x), codes.OrderBy(x => x));
        });
    }

    [Fact]
    public async Task Role_change_takes_effect_after_the_next_refresh()
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"promote{n}@example.com";
        var register = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, fullName = "Promote Me", phoneNumber = $"06{n:D8}" });
        var auth = await register.Content.ReadFromJsonAsync<JsonElement>();
        var oldAccess = auth.GetProperty("accessToken").GetString()!;
        var refresh = auth.GetProperty("refreshToken").GetString()!;
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync("/probe/dispatcher", oldAccess)).StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            var dispatcher = await db.Roles.SingleAsync(r => r.Code == AppRoles.Dispatcher);
            (await db.Users.SingleAsync(u => u.Email == email)).RoleId = dispatcher.Id;
            await db.SaveChangesAsync();
        });

        // The old token still carries the old role until it expires.
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync("/probe/dispatcher", oldAccess)).StatusCode);

        var renewed = await (await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var newAccess = renewed.GetProperty("accessToken").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await GetAsync("/probe/dispatcher", newAccess)).StatusCode);
    }
}
