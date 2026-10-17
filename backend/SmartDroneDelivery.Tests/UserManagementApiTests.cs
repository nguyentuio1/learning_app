using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

// LA-94 complements LA-92 UserManagementTests: web/profile contract compatibility,
// upper input limits, invalid updates and inactive/suspended admin sessions. Core CRUD/audit/role
// matrices are owned by LA-92 and are not duplicated here.
public class UserManagementApiTests : IClassFixture<AuthApiFactory>
{
    private const string Password = "Example123!";
    private static int _counter;
    private readonly AuthApiFactory _factory;

    public UserManagementApiTests(AuthApiFactory factory) => _factory = factory;

    private static Dictionary<string, string> ValidRequest()
    {
        var n = Interlocked.Increment(ref _counter);
        return new()
        {
            ["email"] = $"management{n}@example.com",
            ["password"] = Password,
            ["role"] = "DISPATCHER",
            ["fullName"] = "Test Staff",
            ["phoneNumber"] = $"08{n:D8}"
        };
    }

    [Theory]
    [InlineData("email", "invalid")]
    [InlineData("fullName", " ")]
    [InlineData("phoneNumber", "123")]
    public async Task Invalid_update_is_rejected_without_changing_user_or_audit(string field, string value)
    {
        var (client, _) = await ClientAsync();
        var (targetClient, id) = await ClientAsync("DISPATCHER");
        using (client)
        using (targetClient)
        {
            var input = ValidRequest();
            input[field] = value;
            var response = await client.PutAsJsonAsync($"/api/users/{id}", input);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
            await _factory.WithDbAsync(async db =>
            {
                Assert.Equal("API Test Actor", (await db.Users.SingleAsync(u => u.Id == id)).FullName);
                Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityId == id.ToString() && a.Action == "USER_UPDATE"));
            });
        }
    }

    private async Task<(HttpClient Client, Guid Id)> ClientAsync(string role = "ADMIN", string status = "ACTIVE")
    {
        var client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = ValidRequest();
        var user = new User
        {
            Email = request["email"],
            PhoneNumber = request["phoneNumber"],
            FullName = "API Test Actor",
            PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(Password),
            Role = await db.Roles.SingleAsync(r => r.Code == role),
            Status = status
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var token = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, DateTime.UtcNow);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, user.Id);
    }

    [Theory]
    [InlineData("DISPATCHER")]
    [InlineData("Dispatcher")]
    public async Task Create_hashes_password_persists_profile_returns_detail_location_and_audits(string role)
    {
        var (client, actorId) = await ClientAsync();
        using (client)
        {
            var request = ValidRequest();
            request["email"] = request["email"].ToUpperInvariant();
            request["fullName"] = "  Test Staff  ";
            request["role"] = role;
            var response = await client.PostAsJsonAsync("/api/users", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetGuid();
            Assert.Equal("DISPATCHER", body.GetProperty("roleCode").GetString());
            Assert.Equal("Test Staff", body.GetProperty("fullName").GetString());
            Assert.Equal(request["phoneNumber"], body.GetProperty("phoneNumber").GetString());
            Assert.False(body.TryGetProperty("passwordHash", out _));
            Assert.NotNull(response.Headers.Location);
            Assert.EndsWith($"/api/users/{id}", response.Headers.Location!.ToString());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(response.Headers.Location)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);
            // The parameterized detail route must not intercept the existing profile route.
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);
            await _factory.WithDbAsync(async db =>
            {
                var saved = await db.Users.SingleAsync(u => u.Id == id);
                Assert.Equal(request["email"].ToLowerInvariant(), saved.Email);
                Assert.NotEqual(Password, saved.PasswordHash);
                Assert.True(new PasswordHasher().Verify(Password, saved.PasswordHash));
                var audit = await db.AuditLogs.SingleAsync(a => a.EntityId == id.ToString() && a.Action == "CREATE_USER");
                Assert.Equal(actorId, audit.UserId);
                Assert.Equal("User", audit.EntityName);
            });
            using var loginClient = _factory.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await loginClient.PostAsJsonAsync("/api/auth/login",
                new { email = request["email"], password = Password })).StatusCode);
        }
    }

    [Theory]
    [InlineData("email", 256)]
    [InlineData("password", 101)]
    [InlineData("fullName", 151)]
    [InlineData("phoneNumber", 12)]
    public async Task Overlong_fields_are_rejected(string field, int length)
    {
        var (client, _) = await ClientAsync();
        using (client)
        {
            var request = ValidRequest();
            request[field] = field switch
            {
                "email" => new string('a', length - 12) + "@example.com",
                "password" => "A1" + new string('a', length - 2),
                "phoneNumber" => new string('1', length),
                _ => new string('a', length)
            };
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users", request)).StatusCode);
        }
    }

    [Theory]
    [InlineData("INACTIVE")]
    [InlineData("SUSPENDED")]
    public async Task Inactive_admin_cannot_access_or_mutate_users(string status)
    {
        var (client, _) = await ClientAsync(status: status);
        using (client)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/users/{Guid.NewGuid()}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/users", ValidRequest())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", ValidRequest())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync($"/api/users/{Guid.NewGuid()}/deactivate", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}/role",
                new { role = "MANAGER" })).StatusCode);
        }
    }

}
