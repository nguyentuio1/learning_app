using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Api.Services;

public interface ITokenService
{
    int AccessTokenLifetimeSeconds { get; }
    DateTime RefreshTokenExpiry(DateTime utcNow);
    string CreateAccessToken(User user, DateTime utcNow);
    string CreateRefreshToken();
}

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public int AccessTokenLifetimeSeconds => _options.ExpireMinutes * 60;

    public DateTime RefreshTokenExpiry(DateTime utcNow) => utcNow.AddDays(_options.RefreshTokenExpireDays);

    public string CreateAccessToken(User user, DateTime utcNow)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<System.Security.Claims.Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("role", user.Role.Code),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: utcNow,
            expires: utcNow.AddMinutes(_options.ExpireMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
