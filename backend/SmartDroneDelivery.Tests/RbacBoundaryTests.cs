using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Tests;

// LA-81: execute authorization handlers and the HTTP middleware. The baseline
// RbacTests already verifies the 5x5 role matrix, expiry, issuer and audience.
public class RbacBoundaryTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;
    public RbacBoundaryTests(AuthApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("ADMIN", true)]
    [InlineData("DISPATCHER", true)]
    [InlineData("STATION_OPERATOR", false)]
    [InlineData("admin", false)]
    [InlineData("ADMINISTRATOR", false)]
    public async Task Role_handler_matches_exact_role_codes_not_case_or_prefix(string role, bool allowed)
    {
        var requirement = new RolesAuthorizationRequirement(new[] { "ADMIN", "DISPATCHER" });
        var identity = new ClaimsIdentity(new[] { new Claim("role", role) }, "test", "sub", "role");
        var context = new AuthorizationHandlerContext(new[] { requirement }, new ClaimsPrincipal(identity), null);
        await ((IAuthorizationHandler)requirement).HandleAsync(context);
        Assert.Equal(allowed, context.HasSucceeded);
    }

    private string Token(string? role, string roleClaim = "role", bool future = false, bool signed = true)
    {
        var settings = _factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()) };
        if (role is not null) claims.Add(new Claim(roleClaim, role));
        var now = DateTime.UtcNow;
        var credentials = signed ? new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256) : null;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(settings.Issuer, settings.Audience,
            claims, now.AddMinutes(future ? 5 : -1), now.AddMinutes(15), credentials));
    }

    private async Task<HttpStatusCode> Get(string token, string path, string scheme = "Bearer")
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(scheme, token);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    [Theory]
    [InlineData(null, "role")]
    [InlineData("admin", "role")]
    [InlineData("ADMIN", "roles")]
    [InlineData("CUSTOMER,ADMIN", "role")]
    public async Task Valid_signature_without_exact_admin_claim_is_authenticated_but_forbidden(string? role, string claim)
    {
        var token = Token(role, claim);
        Assert.Equal(HttpStatusCode.OK, await Get(token, "/probe/signed-in"));
        Assert.Equal(HttpStatusCode.Forbidden, await Get(token, "/probe/admin"));
    }

    [Fact]
    public async Task A_future_not_before_token_is_rejected_even_with_admin_role()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await Get(Token("ADMIN", future: true), "/probe/admin"));
    }

    [Fact]
    public async Task An_unsigned_admin_token_is_rejected()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await Get(Token("ADMIN", signed: false), "/probe/admin"));
    }

    [Fact]
    public async Task A_token_sent_under_an_unrecognized_scheme_is_not_authenticated()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await Get(Token("ADMIN"), "/probe/admin", "Basic"));
    }

    [Fact]
    public async Task Query_and_custom_headers_cannot_override_the_signed_customer_role()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe/admin?role=ADMIN");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token("CUSTOMER"));
        request.Headers.Add("X-Role", "ADMIN");
        request.Headers.Add("X-User-Role", "ADMIN");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
