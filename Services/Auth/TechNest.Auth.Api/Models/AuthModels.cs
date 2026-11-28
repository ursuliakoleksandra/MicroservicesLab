using System.ComponentModel.DataAnnotations;

namespace TechNest.Auth.Api.Models;

public sealed record RegisterRequest(
    [Required, StringLength(50, MinimumLength = 3)] string UserName,
    [Required, EmailAddress] string Email,
    [Required] string Password,
    string? FullName);

public sealed record LoginRequest([Required] string UserNameOrEmail, [Required] string Password);

public sealed record EmailRequest([Required, EmailAddress] string Email);

public sealed record VerifyEmailRequest([Required, EmailAddress] string Email, [Required] string Token);

public sealed record ResetPasswordRequest([Required, EmailAddress] string Email, [Required] string Token, [Required] string NewPassword);

public sealed record ChangePasswordRequest([Required] string CurrentPassword, [Required] string NewPassword);

/// <summary>Тіло для refresh/revoke: токен за замовчуванням береться з HttpOnly cookie, тіло — запасний варіант (Postman, тести).</summary>
public sealed record TokenRequest(string? RefreshToken);

public sealed record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, string TokenType = "Bearer");

public sealed record UserDto(string Id, string UserName, string Email, bool EmailConfirmed, string? FullName, string? PhoneNumber, IEnumerable<string> Roles);

public sealed record UpdateUserRequest([StringLength(200)] string? FullName, [Phone] string? PhoneNumber);

public sealed record RoleRequest([Required] string Role);

public sealed record RefreshTokenDto(Guid Id, DateTime CreatedAt, DateTime ExpiresAt, DateTime? RevokedAt, string? RevokedReason, string Status);
