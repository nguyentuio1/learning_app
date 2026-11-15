using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Middleware;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Security;

public class IntegrationKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,
    AppDbContext db,TimeProvider clock,IntegrationRateLimits limits):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    public const string SchemeName="IntegrationKey";
    public const string KeyIdClaim="integration_key_id";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey("X-API-Key") && Request.Headers.ContainsKey("Authorization"))
            throw new ApiException(400,"Do not combine API key and bearer credentials.");
        var values=Request.Headers["X-API-Key"];
        if (values.Count!=1 || string.IsNullOrWhiteSpace(values[0]) || values[0]!.Length>256)
            return AuthenticateResult.NoResult();
        var hash=IntegrationKeyService.Hash(values[0]!);
        var key=await db.IntegrationApiKeys.AsNoTracking().Where(k=>k.KeyHash==hash)
            .Select(k=>new{k.Id,k.CustomerId,k.RevokedAt,k.ExpiresAt}).SingleOrDefaultAsync(Context.RequestAborted);
        if(key==null || key.RevokedAt!=null || key.ExpiresAt<=clock.GetUtcNow().UtcDateTime)
            return AuthenticateResult.Fail("Invalid API key.");
        var create=HttpMethods.IsPost(Request.Method) && string.Equals(Request.Path.Value?.TrimEnd('/'),"/api/v1/delivery-orders",StringComparison.OrdinalIgnoreCase);
        using var lease=await limits.AcquireAsync(key.Id,create,Context.RequestAborted);
        if(!lease.IsAcquired) {
            var seconds=lease.TryGetMetadata(MetadataName.RetryAfter,out var retry)?Math.Max(1,(int)Math.Ceiling(retry.TotalSeconds)):60;
            Response.Headers.RetryAfter=seconds.ToString(CultureInfo.InvariantCulture);
            throw new ApiException(429,"API key request limit exceeded. Retry later.");
        }
        var identity=new ClaimsIdentity(new[]{new Claim(KeyIdClaim,key.Id.ToString()),new Claim(ClaimTypes.NameIdentifier,key.CustomerId.ToString())},SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity),SchemeName));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)=>ApiProblemDetails.WriteAsync(Context,401,"Missing, invalid or expired API key.");
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)=>ApiProblemDetails.WriteAsync(Context,403,"API key access denied.");
}
