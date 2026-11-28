using TechNest.Auth.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Platform.Security;

namespace TechNest.Auth.Api.Services;

/// <summary>Життєвий цикл refresh-токенів: видача, rotation, відкликання.</summary>
public sealed class RefreshTokenService(
    ApplicationDbContext db,
    IJwtTokenService jwt,
    IOptions<JwtSettings> options,
    ILogger<RefreshTokenService> logger)
{
    public enum RotationStatus { Ok, Invalid, Reused, Expired }

    public async Task<string> IssueAsync(ApplicationUser user, string? ip, CancellationToken ct)
    {
        var (token, hash) = jwt.GenerateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(options.Value.RefreshTokenDays),
            CreatedByIp = ip
        });
        await db.SaveChangesAsync(ct);
        return token;
    }

    /// <summary>
    /// Rotation: старий токен відкликається (причина «rotated», посилання на новий), видається новий.
    /// Повторне використання вже відкликаного токена — ознака крадіжки: відкликаються ВСІ активні токени користувача.
    /// </summary>
    public async Task<(RotationStatus Status, ApplicationUser? User, string? NewToken)> RotateAsync(string token, string? ip, CancellationToken ct)
    {
        var hash = jwt.Hash(token);
        var stored = await db.RefreshTokens.Include(t => t.User).SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null) return (RotationStatus.Invalid, null, null);

        if (stored.RevokedAt is not null)
        {
            logger.LogWarning("Revoked refresh token reused for user {UserId} — revoking all active tokens", stored.UserId);
            await RevokeAllAsync(stored.UserId, "reuse_detected", ct);
            return (RotationStatus.Reused, null, null);
        }

        if (stored.IsExpired) return (RotationStatus.Expired, null, null);

        var (newToken, newHash) = jwt.GenerateRefreshToken();
        stored.RevokedAt = DateTime.UtcNow;
        stored.RevokedReason = "rotated";
        stored.ReplacedByTokenHash = newHash;

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = stored.UserId,
            TokenHash = newHash,
            ExpiresAt = DateTime.UtcNow.AddDays(options.Value.RefreshTokenDays),
            CreatedByIp = ip
        });
        await db.SaveChangesAsync(ct);

        return (RotationStatus.Ok, stored.User, newToken);
    }

    /// <summary>Відкликає токен; повертає false, якщо токена немає, він уже відкликаний або належить іншому користувачу.</summary>
    public async Task<bool> RevokeAsync(string token, string? requiredUserId, string reason, CancellationToken ct)
    {
        var hash = jwt.Hash(token);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAt is not null) return false;
        if (requiredUserId is not null && stored.UserId != requiredUserId) return false;

        stored.RevokedAt = DateTime.UtcNow;
        stored.RevokedReason = reason;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task RevokeAllAsync(string userId, string reason, CancellationToken ct)
    {
        var active = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in active)
        {
            t.RevokedAt = DateTime.UtcNow;
            t.RevokedReason = reason;
        }
        await db.SaveChangesAsync(ct);
    }
}
