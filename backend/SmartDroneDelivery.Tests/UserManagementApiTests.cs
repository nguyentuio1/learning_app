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
    [InlineData("GET", "/api/users")]
    [InlineData("GET", "/api/users/11111111-1111-1111-1111-111111111111")]
    [InlineData("POST", "/api/users")]
    [InlineData("PUT", "/api/users/11111111-1111-1111-1111-111111111111")]
    [InlineData("PUT", "/api/users/11111111-1111-1111-1111-111111111111/deactivate")]
    [InlineData("PUT", "/api/users/11111111-1111-1111-1111-111111111111/role")]
    public async Task Endpoints_require_login_and_admin_role(string method, string url)
    {
        using var anonymous = _factory.CreateClient();
        var (customer, _) = await ClientAsync("CUSTOMER");
        using (customer)
        {
            foreach (var (client, expected) in new[]
                     { (anonymous, HttpStatusCode.Unauthorized), (customer, HttpStatusCode.Forbidden) })
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), url);
                if (method == "POST" || method == "PUT")
                    request.Content = JsonContent.Create(ValidRequest());
                var response = await client.SendAsync(request);
                Assert.Equal(expected, response.StatusCode);
                var error = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal((int)expected, error.GetProperty("status").GetInt32());
            }
        }
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
    [InlineData("email", "invalid")]
    [InlineData("password", "Abc123")]
    [InlineData("password", "abcdefgh")]
    [InlineData("password", "12345678")]
    [InlineData("fullName", "   ")]
    [InlineData("phoneNumber", "123")]
    [InlineData("phoneNumber", "abcdefghij")]
    [InlineData("role", "OWNER")]
    public async Task Invalid_fields_return_problem_details_without_persistence(string field, string value)
    {
        var (client, _) = await ClientAsync();
        using (client)
        {
            var request = ValidRequest();
            request[field] = value;
            var response = await client.PostAsJsonAsync("/api/users", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(400, body.GetProperty("status").GetInt32());
            Assert.True(body.GetProperty("errors").TryGetProperty(field, out _));
            await _factory.WithDbAsync(async db => Assert.False(await db.Users.AnyAsync(u => u.Email == request["email"])));
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

    [Fact]
    public async Task Duplicate_email_or_phone_returns_conflict_without_extra_audit()
    {
        var (client, _) = await ClientAsync();
        using (client)
        {
            var first = ValidRequest();
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/users", first)).StatusCode);
            foreach (var field in new[] { "email", "phoneNumber" })
            {
                var duplicate = ValidRequest();
                duplicate[field] = first[field].ToUpperInvariant();
                var response = await client.PostAsJsonAsync("/api/users", duplicate);
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                Assert.Equal(409, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32());
            }
            await _factory.WithDbAsync(async db =>
            {
                var id = (await db.Users.SingleAsync(u => u.Email == first["email"])).Id.ToString();
                Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityId == id && a.Action == "CREATE_USER"));
            });
        }
    }

    [Fact]
    public async Task Role_change_persists_updated_time_and_audits_only_a_real_change()
    {
        var (client, actorId) = await ClientAsync();
        var (targetClient, targetId) = await ClientAsync("DISPATCHER");
        using (client)
        using (targetClient)
        {
            var oldTime = DateTime.UtcNow.AddDays(-1);
            await _factory.WithDbAsync(async db =>
            {
                (await db.Users.SingleAsync(u => u.Id == targetId)).UpdatedAt = oldTime;
                await db.SaveChangesAsync();
            });
            for (var i = 0; i < 2; i++)
                Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/users/{targetId}/role",
                    new { role = "MANAGER" })).StatusCode);
            await _factory.WithDbAsync(async db =>
            {
                var user = await db.Users.Include(u => u.Role).SingleAsync(u => u.Id == targetId);
                Assert.Equal("MANAGER", user.Role.Code);
                Assert.True(user.UpdatedAt > oldTime);
                var audit = await db.AuditLogs.SingleAsync(a => a.EntityId == targetId.ToString() && a.Action == "ROLE_CHANGE");
                Assert.Equal(actorId, audit.UserId);
            });
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/users/{targetId}/role",
                new { role = "OWNER" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}/role",
                new { role = "MANAGER" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/users/{Guid.NewGuid()}")).StatusCode);
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

    [Theory]
    [InlineData("DISPATCHER")]
    [InlineData("STATION_OPERATOR")]
    [InlineData("MANAGER")]
    public async Task Every_non_admin_role_is_forbidden_from_all_management_endpoints(string role)
    {
        var (client, id) = await ClientAsync(role);
        using (client)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/users/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/users", ValidRequest())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/users/{id}", ValidRequest())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/users/{id}/role", new { role = "ADMIN" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync($"/api/users/{id}/deactivate", null)).StatusCode);
        }
    }

    [Fact]
    public async Task Update_persists_normalized_details_ignores_privileged_fields_and_records_actor()
    {
        var (client, actorId) = await ClientAsync();
        var (targetClient, id) = await ClientAsync("DISPATCHER");
        using (client)
        using (targetClient)
        {
            var values = ValidRequest();
            values["email"] = values["email"].ToUpperInvariant();
            values["fullName"] = "  Updated Staff  ";
            values["role"] = "ADMIN";
            values["status"] = "SUSPENDED";
            values["password"] = "Changed999";
            var oldTime = DateTime.UtcNow.AddDays(-1);
            await _factory.WithDbAsync(async db =>
            {
                (await db.Users.SingleAsync(u => u.Id == id)).UpdatedAt = oldTime;
                await db.SaveChangesAsync();
            });
            for (var attempt = 0; attempt < 2; attempt++)
                Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/users/{id}", values)).StatusCode);
            var detail = await (await client.GetAsync($"/api/users/{id}")).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Updated Staff", detail.GetProperty("fullName").GetString());
            Assert.Equal("DISPATCHER", detail.GetProperty("roleCode").GetString());
            Assert.Equal("ACTIVE", detail.GetProperty("status").GetString());
            await _factory.WithDbAsync(async db =>
            {
                var saved = await db.Users.SingleAsync(u => u.Id == id);
                Assert.Equal(values["email"].ToLowerInvariant(), saved.Email);
                Assert.Equal(values["phoneNumber"], saved.PhoneNumber);
                Assert.True(saved.UpdatedAt > oldTime);
                Assert.True(new PasswordHasher().Verify(Password, saved.PasswordHash));
                var audit = await db.AuditLogs.SingleAsync(a => a.EntityId == id.ToString() && a.Action == "USER_UPDATE");
                Assert.Equal(actorId, audit.UserId);
                Assert.Equal("User", audit.EntityName);
            });
        }
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

    [Theory]
    [InlineData("email")]
    [InlineData("phoneNumber")]
    public async Task Update_checks_duplicates_excluding_the_target_user(string field)
    {
        var (client, _) = await ClientAsync();
        using (client)
        {
            var first = ValidRequest();
            var second = ValidRequest();
            await client.PostAsJsonAsync("/api/users", first);
            var created = await (await client.PostAsJsonAsync("/api/users", second)).Content.ReadFromJsonAsync<JsonElement>();
            var id = created.GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/users/{id}", second)).StatusCode);
            second[field] = first[field].ToUpperInvariant();
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/users/{id}", second)).StatusCode);
            await _factory.WithDbAsync(async db =>
                Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityId == id.ToString() && a.Action == "USER_UPDATE")));
        }
    }

    [Fact]
    public async Task Deactivation_retains_user_and_history_revokes_sessions_and_is_idempotent()
    {
        var (client, actorId) = await ClientAsync();
        using (client)
        {
            var input = ValidRequest();
            var created = await (await client.PostAsJsonAsync("/api/users", input)).Content.ReadFromJsonAsync<JsonElement>();
            var id = created.GetProperty("id").GetGuid();
            using var accountClient = _factory.CreateClient();
            var login = await (await accountClient.PostAsJsonAsync("/api/auth/login",
                new { email = input["email"], password = Password })).Content.ReadFromJsonAsync<JsonElement>();
            var refreshToken = login.GetProperty("refreshToken").GetString();
            var oldTime = DateTime.UtcNow.AddDays(-1);
            await _factory.WithDbAsync(async db =>
            {
                (await db.Users.SingleAsync(u => u.Id == id)).UpdatedAt = oldTime;
                await db.SaveChangesAsync();
            });
            for (var attempt = 0; attempt < 2; attempt++)
                Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/users/{id}/deactivate", null)).StatusCode);
            await _factory.WithDbAsync(async db =>
            {
                var saved = await db.Users.SingleAsync(u => u.Id == id);
                Assert.Equal("INACTIVE", saved.Status);
                Assert.True(saved.UpdatedAt > oldTime);
                Assert.True(new PasswordHasher().Verify(Password, saved.PasswordHash));
                Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == id.ToString() && a.Action == "CREATE_USER"));
                var audit = await db.AuditLogs.SingleAsync(a => a.EntityId == id.ToString() && a.Action == "USER_DEACTIVATE");
                Assert.Equal(actorId, audit.UserId);
                Assert.All(await db.RefreshTokens.Where(t => t.UserId == id).ToListAsync(), token => Assert.True(token.IsRevoked));
            });
            Assert.Equal(HttpStatusCode.Forbidden, (await accountClient.PostAsJsonAsync("/api/auth/login",
                new { email = input["email"], password = Password })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await accountClient.PostAsJsonAsync("/api/auth/refresh", new { refreshToken })).StatusCode);
            accountClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
            Assert.Equal(HttpStatusCode.Forbidden, (await accountClient.GetAsync("/api/users/me")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/users/{id}")).StatusCode);
        }
    }

    [Fact]
    public async Task Missing_update_and_deactivation_targets_return_not_found()
    {
        var (client, _) = await ClientAsync();
        using (client)
        {
            var id = Guid.NewGuid();
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/users/{id}", ValidRequest())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsync($"/api/users/{id}/deactivate", null)).StatusCode);
        }
    }
}
