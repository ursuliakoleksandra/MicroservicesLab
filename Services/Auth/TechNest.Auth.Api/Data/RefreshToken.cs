namespace TechNest.Auth.Api.Data;

/// <summary>
/// Refresh-токен у БД. Зберігається лише SHA-256 хеш значення (самого токена в БД немає — як із паролем).
/// Rotation: при оновленні старий токен відкликається (RevokedAt, ReplacedByTokenHash), створюється новий.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;

    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? RevokedReason { get; set; }
    public string? CreatedByIp { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => RevokedAt is null && !IsExpired;
}
