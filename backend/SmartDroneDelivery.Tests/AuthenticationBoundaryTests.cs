using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

// LA-79 supplements AuthApiTests: real validation boundaries, rejected-request
// persistence, privilege injection and password/token primitives (not reflection).
public class AuthenticationBoundaryTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;
    public AuthenticationBoundaryTests(AuthApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData(8, true)]
    [InlineData(100, true)]
    [InlineData(7, false)]
    [InlineData(101, false)]
    public void Password_length_boundaries_are_validated(int length, bool valid)
    {
        var request = new RegisterRequest { Email = "boundary@example.com", FullName = "Boundary",
            PhoneNumber = "0901234567", Password = "A1" + new string('x', length - 2) };
        var errors = new List<ValidationResult>();
        Assert.Equal(valid, Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        if (!valid) Assert.Contains(errors, e => e.MemberNames.Contains(nameof(RegisterRequest.Password)));
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(11, true)]
    [InlineData(8, false)]
    [InlineData(12, false)]
    public void Phone_length_boundaries_are_validated(int length, bool valid)
    {
        var request = new RegisterRequest { Email = "boundary@example.com", FullName = "Boundary",
            PhoneNumber = new string('1', length), Password = "Passw0rd123" };
        var errors = new List<ValidationResult>();
        Assert.Equal(valid, Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        if (!valid) Assert.Contains(errors, e => e.MemberNames.Contains(nameof(RegisterRequest.PhoneNumber)));
    }

    [Fact]
    public void Hashes_are_salted_and_verify_only_the_matching_password()
    {
        var hasher = new PasswordHasher();
        var first = hasher.Hash("Passw0rd123");
        var second = hasher.Hash("Passw0rd123");
        Assert.NotEqual(first, second);
        Assert.True(hasher.Verify("Passw0rd123", first));
        Assert.True(hasher.Verify("Passw0rd123", second));
        Assert.False(hasher.Verify("WrongPass123", first));
        Assert.False(hasher.Verify("Passw0rd123", "invalid-hash"));
    }

    [Fact]
    public void Token_lifetimes_follow_configuration_and_refresh_values_are_url_safe()
    {
        var service = new TokenService(Options.Create(new JwtOptions { ExpireMinutes = 3, RefreshTokenExpireDays = 2 }));
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(180, service.AccessTokenLifetimeSeconds);
        Assert.Equal(now.AddDays(2), service.RefreshTokenExpiry(now));
        var tokens = Enumerable.Range(0, 20).Select(_ => service.CreateRefreshToken()).ToArray();
        Assert.Equal(tokens.Length, tokens.Distinct().Count());
        Assert.All(tokens, token => Assert.Matches("^[A-Za-z0-9_-]{43}$", token));
    }

    [Theory]
    [InlineData("fullName")]
    [InlineData("password")]
    [InlineData("email")]
    public async Task Overlong_register_fields_return_400_without_persisting_any_identity(string field)
    {
        using var isolated = new AuthApiFactory();
        using var client = isolated.CreateClient();
        var body = new Dictionary<string, string> { ["email"] = "length@example.com", ["password"] = "Passw0rd123",
            ["fullName"] = "Boundary", ["phoneNumber"] = "0903214567" };
        body[field] = field switch { "fullName" => new string('N', 151), "password" => "A1" + new string('x', 99),
            _ => new string('a', 244) + "@example.com" };
        int users = 0, tokens = 0, audits = 0;
        await isolated.WithDbAsync(async db => { users = await db.Users.CountAsync(); tokens = await db.RefreshTokens.CountAsync(); audits = await db.AuditLogs.CountAsync(); });
        using var response = await client.PostAsJsonAsync("/api/auth/register", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(error.GetProperty("errors").GetProperty(field).EnumerateArray());
        await isolated.WithDbAsync(async db => { Assert.Equal(users, await db.Users.CountAsync()); Assert.Equal(tokens, await db.RefreshTokens.CountAsync()); Assert.Equal(audits, await db.AuditLogs.CountAsync()); });
    }

    [Fact]
    public async Task Public_registration_ignores_injected_staff_role_status_and_password_hash()
    {
        using var client = _factory.CreateClient();
        var email = $"injection-{Guid.NewGuid():N}@example.com";
        using var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "Passw0rd123",
            fullName = "Customer", phoneNumber = "0908111222", role = "ADMIN", roleId = 5, status = "SUSPENDED", passwordHash = "injected" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user");
        Assert.Equal("CUSTOMER", user.GetProperty("role").GetProperty("code").GetString());
        Assert.Equal("ACTIVE", user.GetProperty("status").GetString());
        Assert.False(user.TryGetProperty("passwordHash", out _));
        await _factory.WithDbAsync(async db => { var saved = await db.Users.Include(u => u.Role).SingleAsync(u => u.Email == email);
            Assert.Equal("CUSTOMER", saved.Role.Code); Assert.Equal("ACTIVE", saved.Status);
            Assert.True(new PasswordHasher().Verify("Passw0rd123", saved.PasswordHash)); });
    }

    [Theory]
    [InlineData("email", "Email is required.")]
    [InlineData("password", "Password is required.")]
    public async Task Blank_login_fields_return_field_specific_errors(string field, string message)
    {
        using var client = _factory.CreateClient();
        var body = new Dictionary<string, string> { ["email"] = "nobody@example.com", ["password"] = "Passw0rd123" };
        body[field] = "   ";
        using var response = await client.PostAsJsonAsync("/api/auth/login", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Contains(message, errors.GetProperty(field).EnumerateArray().Select(e => e.GetString()));
    }
}
