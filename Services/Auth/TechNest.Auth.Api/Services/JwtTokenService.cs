using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TechNest.Auth.Api.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Platform.Security;

namespace TechNest.Auth.Api.Services;

public sealed class JwtTokenService(IOptions<JwtSettings> options) : IJwtTokenService
{
    private readonly JwtSettings _settings = options.Value;

    private SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(_settings.SecretKey));

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName!),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        // коротке ім'я claim "role" (а не довгий ClaimTypes.Role) — так ролі читає й будь-який інший сервіс/мова
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(Key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public (string Token, string Hash) GenerateRefreshToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (token, Hash(token));
    }

    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public ClaimsPrincipal? ValidateToken(string accessToken)
    {
        try
        {
            return new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(accessToken, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = _settings.Issuer,
                ValidateAudience = true, ValidAudience = _settings.Audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = Key,
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "unique_name", RoleClaimType = "role"
            }, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
