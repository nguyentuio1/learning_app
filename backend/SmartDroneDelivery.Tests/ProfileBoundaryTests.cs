using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.DTOs;

namespace SmartDroneDelivery.Tests;

// LA-85 supplements ProfileApiTests without replacing its upload/password tests.
public class ProfileBoundaryTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;
    private static int _counter;
    public ProfileBoundaryTests(AuthApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData(150, true)]
    [InlineData(151, false)]
    [InlineData(0, false)]
    public void Profile_name_boundaries_use_real_validation(int length, bool valid)
    {
        var request = new UpdateProfileRequest { FullName = new string('N', length), PhoneNumber = "0901234567" };
        var errors = new List<ValidationResult>();
        Assert.Equal(valid, Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        if (!valid) Assert.Contains(errors, e => e.MemberNames.Contains(nameof(UpdateProfileRequest.FullName)));
    }

    [Fact]
    public void Password_change_requires_both_fields_with_actionable_messages()
    {
        var request = new ChangePasswordRequest();
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        Assert.Contains(errors, e => e.MemberNames.Contains("CurrentPassword") && e.ErrorMessage == "Current password is required.");
        Assert.Contains(errors, e => e.MemberNames.Contains("NewPassword") && e.ErrorMessage == "New password is required.");
    }

    private async Task<JsonElement> Account(HttpClient client)
    {
        var n = Interlocked.Increment(ref _counter);
        using var response = await client.PostAsJsonAsync("/api/auth/register", new {
            email = $"profile-boundary-{n}@example.com", password = "Passw0rd123", fullName = "Original Name", phoneNumber = $"03{n:D8}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, JsonElement auth, HttpMethod method, string path, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Injected_user_id_cannot_update_another_accounts_profile()
    {
        using var client = _factory.CreateClient();
        var alice = await Account(client);
        var bob = await Account(client);
        var bobId = bob.GetProperty("user").GetProperty("id").GetGuid();
        using var response = await Send(client, alice, HttpMethod.Put, "/api/users/me", JsonContent.Create(new {
            id = bobId, userId = bobId, fullName = "Alice Updated", phoneNumber = alice.GetProperty("user").GetProperty("phoneNumber").GetString() }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(alice.GetProperty("user").GetProperty("id").GetGuid(), updated.GetProperty("id").GetGuid());
        await _factory.WithDbAsync(async db => { var untouched = await db.Users.SingleAsync(u => u.Id == bobId);
            Assert.Equal("Original Name", untouched.FullName); Assert.Equal(bob.GetProperty("user").GetProperty("phoneNumber").GetString(), untouched.PhoneNumber); });
    }

    [Fact]
    public async Task Rejected_profile_validation_does_not_partially_save_the_valid_fields()
    {
        using var client = _factory.CreateClient();
        var auth = await Account(client);
        using var response = await Send(client, auth, HttpMethod.Put, "/api/users/me", JsonContent.Create(new { fullName = "Must not save", phoneNumber = "bad" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var get = await Send(client, auth, HttpMethod.Get, "/api/users/me");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var user = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Original Name", user.GetProperty("fullName").GetString());
        Assert.Equal(auth.GetProperty("user").GetProperty("phoneNumber").GetString(), user.GetProperty("phoneNumber").GetString());
    }

    [Theory]
    [InlineData("GET", "/api/users/me")]
    [InlineData("PUT", "/api/users/me")]
    [InlineData("PUT", "/api/users/me/password")]
    [InlineData("POST", "/api/users/me/avatar")]
    public async Task Suspended_user_cannot_read_or_mutate_profile_with_an_existing_token(string method, string path)
    {
        using var client = _factory.CreateClient();
        var auth = await Account(client);
        var id = auth.GetProperty("user").GetProperty("id").GetGuid();
        await _factory.WithDbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == id)).Status = "SUSPENDED"; await db.SaveChangesAsync(); });
        HttpContent? content = null;
        if (path.EndsWith("/avatar"))
        {
            var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF }), "file", "avatar.jpg");
            content = form;
        }
        else if (method == "PUT") content = path.EndsWith("/password")
            ? JsonContent.Create(new { currentPassword = "Passw0rd123", newPassword = "NewPassw0rd456" })
            : JsonContent.Create(new { fullName = "Updated", phoneNumber = auth.GetProperty("user").GetProperty("phoneNumber").GetString() });
        using var response = await Send(client, auth, new HttpMethod(method), path, content);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await _factory.WithDbAsync(async db => { var user = await db.Users.SingleAsync(u => u.Id == id);
            Assert.Equal("Original Name", user.FullName); Assert.Null(user.AvatarUrl);
            Assert.False(await db.AuditLogs.AnyAsync(a => a.UserId == id && a.Action == "PASSWORD_CHANGE")); });
    }

    [Fact]
    public async Task Wrong_current_password_keeps_refresh_session_usable_and_creates_no_change_audit()
    {
        using var client = _factory.CreateClient();
        var auth = await Account(client);
        using var response = await Send(client, auth, HttpMethod.Put, "/api/users/me/password", JsonContent.Create(new { currentPassword = "WrongPass123", newPassword = "NewPassw0rd456" }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.GetProperty("refreshToken").GetString() });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var id = auth.GetProperty("user").GetProperty("id").GetGuid();
        await _factory.WithDbAsync(async db => Assert.False(await db.AuditLogs.AnyAsync(a => a.UserId == id && a.Action == "PASSWORD_CHANGE")));
    }

    [Fact]
    public async Task Wrong_multipart_field_is_rejected_without_storage_write()
    {
        using var client = _factory.CreateClient();
        var auth = await Account(client);
        var count = _factory.Avatars.Files.Count;
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF }), "avatar", "avatar.jpg");
        using var response = await Send(client, auth, HttpMethod.Post, "/api/users/me/avatar", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Contains("A file is required.", errors.GetProperty("file").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(count, _factory.Avatars.Files.Count);
    }
}
