using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

// LA-130: HTTP authorization boundary cases complement the existing management tests.
// Real routing, authentication, authorization and EF services; isolated in-memory DB.
public class DroneFleetAccessTests : IClassFixture<AuthApiFactory>, IDisposable
{
    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;
    public DroneFleetAccessTests(AuthApiFactory factory) { _factory = factory; _client = factory.CreateClient(); }
    public void Dispose() => _client.Dispose();

    private async Task<string> TokenAsync(string roleCode, bool deactivate = false)
    {
        var id = Guid.NewGuid();
        var email = $"la130-{id:N}@example.com";
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
    [InlineData("GET", "/api/drones")]
    [InlineData("GET", "/api/drones/{id}")]
    [InlineData("POST", "/api/drones")]
    [InlineData("PUT", "/api/drones/{id}")]
    [InlineData("PUT", "/api/drones/{id}/maintenance")]
    public async Task Anonymous_requests_are_rejected_before_resource_lookup(string method, string path)
    {
        using var response = await SendAsync(method, path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [Theory]
    [InlineData("CUSTOMER", "GET", "/api/drones")]
    [InlineData("CUSTOMER", "GET", "/api/drones/{id}")]
    [InlineData("CUSTOMER", "POST", "/api/drones")]
    [InlineData("CUSTOMER", "PUT", "/api/drones/{id}")]
    [InlineData("CUSTOMER", "PUT", "/api/drones/{id}/maintenance")]
    [InlineData("MANAGER", "GET", "/api/drones")]
    [InlineData("MANAGER", "GET", "/api/drones/{id}")]
    [InlineData("MANAGER", "POST", "/api/drones")]
    [InlineData("MANAGER", "PUT", "/api/drones/{id}")]
    [InlineData("MANAGER", "PUT", "/api/drones/{id}/maintenance")]
    [InlineData("STATION_OPERATOR", "GET", "/api/drones")]
    [InlineData("STATION_OPERATOR", "GET", "/api/drones/{id}")]
    [InlineData("STATION_OPERATOR", "POST", "/api/drones")]
    [InlineData("STATION_OPERATOR", "PUT", "/api/drones/{id}")]
    [InlineData("STATION_OPERATOR", "PUT", "/api/drones/{id}/maintenance")]
    public async Task Disallowed_roles_cannot_bypass_authorization_with_direct_http(string role, string method, string path)
    {
        var token = await TokenAsync(role);
        using var response = await SendAsync(method, path, token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    [Theory]
    [InlineData("GET", "/api/drones")]
    [InlineData("GET", "/api/drones/{id}")]
    [InlineData("POST", "/api/drones")]
    [InlineData("PUT", "/api/drones/{id}")]
    [InlineData("PUT", "/api/drones/{id}/maintenance")]
    public async Task Deactivated_admin_cannot_reuse_an_existing_access_token(string method, string path)
    {
        var token = await TokenAsync("ADMIN", deactivate: true);
        using var response = await SendAsync(method, path, token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
