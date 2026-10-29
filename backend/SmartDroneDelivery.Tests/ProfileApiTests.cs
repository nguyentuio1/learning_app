using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SmartDroneDelivery.Tests;

public class ProfileApiTests : IClassFixture<AuthApiFactory>
{
    private const string Password = "Passw0rd123";
    private static int _counter;

    private static readonly byte[] PngBytes =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };

    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public ProfileApiTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ---------- helpers ----------

    private record Account(string Email, string Phone, string Access, string Refresh, Guid Id);

    private async Task<Account> NewAccountAsync()
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"profile{n}@example.com";
        var phone = $"05{n:D8}";
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, fullName = "Nguyen Van An", phoneNumber = phone });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Account(email, phone,
            auth.GetProperty("accessToken").GetString()!,
            auth.GetProperty("refreshToken").GetString()!,
            Guid.Parse(auth.GetProperty("user").GetProperty("id").GetString()!));
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? bearer, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> UploadAsync(string? bearer, byte[] bytes, string fileName, string contentType, string field = "file")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, field, fileName);
        return SendAsync(HttpMethod.Post, "/api/users/me/avatar", bearer, form);
    }

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    // ---------- GET /api/users/me ----------

    [Fact]
    public async Task Get_returns_the_profile_of_the_signed_in_user()
    {
        var a = await NewAccountAsync();

        var response = await SendAsync(HttpMethod.Get, "/api/users/me", a.Access);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(a.Id.ToString(), Str(user, "id"));
        Assert.Equal(a.Email, Str(user, "email"));
        Assert.Equal("CUSTOMER", user.GetProperty("role").GetProperty("code").GetString());
        Assert.False(user.TryGetProperty("passwordHash", out _));
    }

    [Theory]
    [InlineData("GET", "/api/users/me")]
    [InlineData("PUT", "/api/users/me")]
    [InlineData("PUT", "/api/users/me/password")]
    [InlineData("POST", "/api/users/me/avatar")]
    public async Task Every_profile_endpoint_requires_a_token(string method, string url)
    {
        var response = await SendAsync(new HttpMethod(method), url, null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_account_gets_403_even_with_a_valid_token()
    {
        var a = await NewAccountAsync();
        await _factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == a.Id)).Status = "SUSPENDED";
            await db.SaveChangesAsync();
        });

        var response = await SendAsync(HttpMethod.Get, "/api/users/me", a.Access);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- PUT /api/users/me ----------

    [Fact]
    public async Task Update_changes_name_and_phone_and_persists_them()
    {
        var a = await NewAccountAsync();
        var newPhone = $"04{Interlocked.Increment(ref _counter):D8}";

        var response = await SendAsync(HttpMethod.Put, "/api/users/me", a.Access,
            JsonContent.Create(new { fullName = "  Tran Thi Binh  ", phoneNumber = newPhone }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Tran Thi Binh", Str(user, "fullName"));
        Assert.Equal(newPhone, Str(user, "phoneNumber"));

        var again = await (await SendAsync(HttpMethod.Get, "/api/users/me", a.Access)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Tran Thi Binh", Str(again, "fullName"));
    }

    [Fact]
    public async Task Update_ignores_email_and_role_in_the_body()
    {
        var a = await NewAccountAsync();

        var response = await SendAsync(HttpMethod.Put, "/api/users/me", a.Access, JsonContent.Create(new
        {
            fullName = "Same Person",
            phoneNumber = a.Phone,
            email = "hijack@example.com",
            roleId = 5,
            role = new { id = 5, code = "ADMIN" },
            status = "ACTIVE"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(a.Email, Str(user, "email"));
        Assert.Equal("CUSTOMER", user.GetProperty("role").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Update_can_keep_the_own_phone_number()
    {
        var a = await NewAccountAsync();

        var response = await SendAsync(HttpMethod.Put, "/api/users/me", a.Access,
            JsonContent.Create(new { fullName = "New Name", phoneNumber = a.Phone }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_with_a_phone_of_another_user_gets_409()
    {
        var a = await NewAccountAsync();
        var b = await NewAccountAsync();

        var response = await SendAsync(HttpMethod.Put, "/api/users/me", a.Access,
            JsonContent.Create(new { fullName = "Name", phoneNumber = b.Phone }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Phone number is already registered.",
            Str(await response.Content.ReadFromJsonAsync<JsonElement>(), "detail"));
    }

    [Theory]
    [InlineData("", "0901234567", "fullName")]
    [InlineData("Name", "123", "phoneNumber")]
    [InlineData("Name", "09abc12345", "phoneNumber")]
    public async Task Update_rejects_invalid_input_with_field_errors(string fullName, string phone, string field)
    {
        var a = await NewAccountAsync();

        var response = await SendAsync(HttpMethod.Put, "/api/users/me", a.Access,
            JsonContent.Create(new { fullName, phoneNumber = phone }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Update_rejects_a_name_longer_than_150_characters()
    {
        var a = await NewAccountAsync();

        var response = await SendAsync(HttpMethod.Put, "/api/users/me", a.Access,
            JsonContent.Create(new { fullName = new string('a', 151), phoneNumber = a.Phone }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- PUT /api/users/me/password ----------

    [Fact]
    public async Task Change_password_works_and_the_new_password_can_log_in()
    {
        var a = await NewAccountAsync();

        var response = await SendAsync(HttpMethod.Put, "/api/users/me/password", a.Access,
            JsonContent.Create(new { currentPassword = Password, newPassword = "NewPassw0rd456" }));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var oldLogin = await _client.PostAsJsonAsync("/api/auth/login", new { email = a.Email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await _client.PostAsJsonAsync("/api/auth/login", new { email = a.Email, password = "NewPassw0rd456" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Change_password_revokes_all_refresh_tokens_of_the_user()
    {
        var a = await NewAccountAsync();
        // A second device signs in.
        var second = await (await _client.PostAsJsonAsync("/api/auth/login", new { email = a.Email, password = Password }))
            .Content.ReadFromJsonAsync<JsonElement>();

        await SendAsync(HttpMethod.Put, "/api/users/me/password", a.Access,
            JsonContent.Create(new { currentPassword = Password, newPassword = "NewPassw0rd456" }));

        foreach (var token in new[] { a.Refresh, Str(second, "refreshToken") })
        {
            var refresh = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token });
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
    }

    [Fact]
    public async Task Change_password_does_not_revoke_tokens_of_other_users()
    {
        var a = await NewAccountAsync();
        var other = await NewAccountAsync();

        await SendAsync(HttpMethod.Put, "/api/users/me/password", a.Access,
            JsonContent.Create(new { currentPassword = Password, newPassword = "NewPassw0rd456" }));

        var refresh = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = other.Refresh });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [Fact]
    public async Task Change_password_writes_an_audit_log_and_stores_a_new_hash()
    {
        var a = await NewAccountAsync();
        string oldHash = "";
        await _factory.WithDbAsync(async db => oldHash = (await db.Users.SingleAsync(u => u.Id == a.Id)).PasswordHash);

        await SendAsync(HttpMethod.Put, "/api/users/me/password", a.Access,
            JsonContent.Create(new { currentPassword = Password, newPassword = "NewPassw0rd456" }));

        await _factory.WithDbAsync(async db =>
        {
            Assert.True(await db.AuditLogs.AnyAsync(l => l.UserId == a.Id && l.Action == "PASSWORD_CHANGE"));
            var hash = (await db.Users.SingleAsync(u => u.Id == a.Id)).PasswordHash;
            Assert.NotEqual(oldHash, hash);
            Assert.DoesNotContain("NewPassw0rd456", hash);
        });
    }

    [Fact]
    public async Task Change_password_with_a_wrong_current_password_gets_401_and_changes_nothing()
    {
        var a = await NewAccountAsync();

        var response = await SendAsync(HttpMethod.Put, "/api/users/me/password", a.Access,
            JsonContent.Create(new { currentPassword = "Wrong-pass1", newPassword = "NewPassw0rd456" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email = a.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public async Task Change_password_rejects_a_weak_new_password_with_400(string weak)
    {
        var a = await NewAccountAsync();

        var response = await SendAsync(HttpMethod.Put, "/api/users/me/password", a.Access,
            JsonContent.Create(new { currentPassword = Password, newPassword = weak }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("newPassword", out _));
    }

    [Fact]
    public async Task Change_password_rejects_the_same_password_with_400()
    {
        var a = await NewAccountAsync();

        var response = await SendAsync(HttpMethod.Put, "/api/users/me/password", a.Access,
            JsonContent.Create(new { currentPassword = Password, newPassword = Password }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("newPassword", out _));
    }

    // ---------- POST /api/users/me/avatar ----------

    [Fact]
    public async Task Upload_a_png_stores_it_and_saves_the_url_on_the_user()
    {
        var a = await NewAccountAsync();

        var response = await UploadAsync(a.Access, PngBytes, "me.png", "image/png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var url = Str(await response.Content.ReadFromJsonAsync<JsonElement>(), "avatarUrl");
        Assert.EndsWith($"/avatars/{a.Id}.png", url);
        Assert.Equal(PngBytes, _factory.Avatars.Files[url].Bytes);
        Assert.Equal("image/png", _factory.Avatars.Files[url].ContentType);

        var profile = await (await SendAsync(HttpMethod.Get, "/api/users/me", a.Access)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(url, Str(profile, "avatarUrl"));
    }

    [Fact]
    public async Task Upload_a_jpeg_is_accepted()
    {
        var a = await NewAccountAsync();

        var response = await UploadAsync(a.Access, JpegBytes, "me.jpg", "image/jpeg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.EndsWith($"/avatars/{a.Id}.jpg", Str(await response.Content.ReadFromJsonAsync<JsonElement>(), "avatarUrl"));
    }

    [Fact]
    public async Task Replacing_a_png_by_a_jpeg_deletes_the_old_file()
    {
        var a = await NewAccountAsync();
        var first = Str(await (await UploadAsync(a.Access, PngBytes, "me.png", "image/png")).Content.ReadFromJsonAsync<JsonElement>(), "avatarUrl");

        var second = Str(await (await UploadAsync(a.Access, JpegBytes, "me.jpg", "image/jpeg")).Content.ReadFromJsonAsync<JsonElement>(), "avatarUrl");

        Assert.NotEqual(first, second);
        Assert.Contains(first, _factory.Avatars.Deleted);
        Assert.DoesNotContain(first, _factory.Avatars.Files.Keys);
        Assert.Contains(second, _factory.Avatars.Files.Keys);
    }

    [Fact]
    public async Task Uploading_the_same_type_again_overwrites_without_deleting()
    {
        var a = await NewAccountAsync();
        await UploadAsync(a.Access, PngBytes, "me.png", "image/png");
        var deletedBefore = _factory.Avatars.Deleted.Count;

        var response = await UploadAsync(a.Access, PngBytes, "again.png", "image/png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(deletedBefore, _factory.Avatars.Deleted.Count);
    }

    [Fact]
    public async Task Upload_checks_the_file_content_not_the_name_or_content_type()
    {
        var a = await NewAccountAsync();
        var notAnImage = System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>");

        var disguised = await UploadAsync(a.Access, notAnImage, "me.png", "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, disguised.StatusCode);
        var errors = (await disguised.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("file", out _));
    }

    [Fact]
    public async Task Upload_rejects_other_image_formats_such_as_gif()
    {
        var a = await NewAccountAsync();
        var gif = System.Text.Encoding.ASCII.GetBytes("GIF89a\0\0\0\0\0\0");

        var response = await UploadAsync(a.Access, gif, "me.gif", "image/gif");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_a_file_larger_than_5_MB_and_accepts_exactly_5_MB()
    {
        var a = await NewAccountAsync();

        var tooBig = new byte[5 * 1024 * 1024 + 1];
        PngBytes.CopyTo(tooBig, 0);
        var rejected = await UploadAsync(a.Access, tooBig, "big.png", "image/png");
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var limit = new byte[5 * 1024 * 1024];
        PngBytes.CopyTo(limit, 0);
        var accepted = await UploadAsync(a.Access, limit, "limit.png", "image/png");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task Upload_without_a_file_or_with_an_empty_file_gets_400()
    {
        var a = await NewAccountAsync();

        var wrongField = await UploadAsync(a.Access, PngBytes, "me.png", "image/png", field: "picture");
        Assert.Equal(HttpStatusCode.BadRequest, wrongField.StatusCode);

        var empty = await UploadAsync(a.Access, Array.Empty<byte>(), "empty.png", "image/png");
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task A_failed_upload_leaves_the_existing_avatar_untouched()
    {
        var a = await NewAccountAsync();
        var url = Str(await (await UploadAsync(a.Access, PngBytes, "me.png", "image/png")).Content.ReadFromJsonAsync<JsonElement>(), "avatarUrl");

        await UploadAsync(a.Access, new byte[] { 1, 2, 3 }, "bad.png", "image/png");

        var profile = await (await SendAsync(HttpMethod.Get, "/api/users/me", a.Access)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(url, Str(profile, "avatarUrl"));
    }
}
