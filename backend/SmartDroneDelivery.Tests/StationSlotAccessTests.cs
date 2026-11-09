using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

// LA-120: HTTP authorization boundary cases complement the existing management tests.
// Real routing, authentication, authorization and EF services; isolated in-memory DB.
public class StationSlotAccessTests : IClassFixture<AuthApiFactory>, IDisposable
{
    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;
    public StationSlotAccessTests(AuthApiFactory factory) { _factory = factory; _client = factory.CreateClient(); }
    public void Dispose() => _client.Dispose();

    private async Task<string> TokenAsync(string roleCode, bool deactivate = false)
    {
        var id = Guid.NewGuid();
        var email = $"la120-{id:N}@example.com";
        const string password = "TestOnly!12345";
        await _factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.SingleAsync(r => r.Code == roleCode);
            db.Users.Add(new User { Id = id, RoleId = role.Id, Email = email,
                FullName = "Boundary test", PhoneNumber = "0901234567",
                PasswordHash = new PasswordHasher().Hash(password), Status = "ACTIVE" });
            await db.SaveChangesAsync();
        });
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (deactivate) await _factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == id);
            user.Status = "INACTIVE";
            await db.SaveChangesAsync();
        });
        return json.GetProperty("accessToken").GetString()!;
    }
    private async Task<HttpResponseMessage> SendAsync(string method, string path, string? token = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path.Replace("{id}", Guid.NewGuid().ToString()));
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(new { });
        return await _client.SendAsync(request);
    }
    [Theory]
    [InlineData("GET", "/api/landing-stations/{id}/slots")]
    [InlineData("GET", "/api/station-slots/{id}")]
    [InlineData("POST", "/api/landing-stations/{id}/slots")]
    [InlineData("DELETE", "/api/station-slots/{id}")]
    [InlineData("PUT", "/api/station-slots/{id}/status")]
    public async Task Anonymous_requests_are_rejected_before_resource_lookup(string method, string path)
    {
        using var response = await SendAsync(method, path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [Theory]
    [InlineData("CUSTOMER", "GET", "/api/landing-stations/{id}/slots")]
    [InlineData("CUSTOMER", "GET", "/api/station-slots/{id}")]
    [InlineData("CUSTOMER", "POST", "/api/landing-stations/{id}/slots")]
    [InlineData("CUSTOMER", "DELETE", "/api/station-slots/{id}")]
    [InlineData("CUSTOMER", "PUT", "/api/station-slots/{id}/status")]
    [InlineData("MANAGER", "GET", "/api/landing-stations/{id}/slots")]
    [InlineData("MANAGER", "GET", "/api/station-slots/{id}")]
    [InlineData("MANAGER", "POST", "/api/landing-stations/{id}/slots")]
    [InlineData("MANAGER", "DELETE", "/api/station-slots/{id}")]
    [InlineData("MANAGER", "PUT", "/api/station-slots/{id}/status")]
    public async Task Disallowed_roles_cannot_bypass_authorization_with_direct_http(string role, string method, string path)
    {
        var token = await TokenAsync(role);
        using var response = await SendAsync(method, path, token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    [Theory]
    [InlineData("GET", "/api/landing-stations/{id}/slots")]
    [InlineData("GET", "/api/station-slots/{id}")]
    [InlineData("POST", "/api/landing-stations/{id}/slots")]
    [InlineData("DELETE", "/api/station-slots/{id}")]
    [InlineData("PUT", "/api/station-slots/{id}/status")]
    public async Task Deactivated_admin_cannot_reuse_an_existing_access_token(string method, string path)
    {
        var token = await TokenAsync("ADMIN", deactivate: true);
        using var response = await SendAsync(method, path, token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
