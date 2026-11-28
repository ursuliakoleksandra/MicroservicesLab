using System.Security.Claims;
using TechNest.Auth.Api.Data;

namespace TechNest.Auth.Api.Services;

public interface IJwtTokenService
{
    /// <summary>Access token (15 хв): claims sub, unique_name, role (+ email, jti).</summary>
    (string Token, DateTime ExpiresAt) GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);

    /// <summary>Криптографічно випадковий refresh-токен: (значення для клієнта, SHA-256 хеш для БД).</summary>
    (string Token, string Hash) GenerateRefreshToken();

    /// <summary>Хеш refresh-токена для пошуку в БД.</summary>
    string Hash(string token);

    /// <summary>Перевіряє підпис, issuer, audience і термін дії; null — токен недійсний.</summary>
    ClaimsPrincipal? ValidateToken(string accessToken);
}
