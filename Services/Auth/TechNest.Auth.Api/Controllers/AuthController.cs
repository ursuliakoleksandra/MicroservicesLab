using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TechNest.Auth.Api.Data;
using TechNest.Auth.Api.Models;
using TechNest.Auth.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Platform.Security;

namespace TechNest.Auth.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IJwtTokenService jwt,
    RefreshTokenService refreshTokens,
    IAuthEmailSender email,
    IOptions<JwtSettings> jwtOptions,
    IWebHostEnvironment env,
    ILogger<AuthController> logger) : ControllerBase
{
    public const string RefreshCookie = "refreshToken";

    // ---------- Реєстрація та email ----------

    /// <summary>Реєстрація: користувач отримує роль User; лист із токеном підтвердження (mock — у лог). Без підтвердження login заблоковано.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = new ApplicationUser { UserName = request.UserName, Email = request.Email, FullName = request.FullName };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return IdentityProblem(result);

        await users.AddToRoleAsync(user, Roles.User);

        var token = await SendVerificationEmailAsync(user, ct);
        logger.LogInformation("User {UserId} ({UserName}) registered", user.Id, user.UserName);

        // у Development токен повертається й у відповіді — щоб не шукати його в логах під час демонстрації
        return StatusCode(StatusCodes.Status201Created, new
        {
            userId = user.Id,
            email = user.Email,
            message = "Registered. Confirm the email before login.",
            devVerificationToken = env.IsDevelopment() ? token : null
        });
    }

    /// <summary>Підтвердження email за одноразовим токеном (після використання токен недійсний — змінюється security stamp).</summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null) return BadRequestProblem("Invalid verification request.");

        var result = await users.ConfirmEmailAsync(user, Decode(request.Token));
        if (!result.Succeeded) return BadRequestProblem("Invalid or expired verification token.");

        logger.LogInformation("Email confirmed for user {UserId}", user.Id);
        return Ok(new { message = "Email confirmed." });
    }

    /// <summary>Повторна відправка листа підтвердження. Відповідь однакова незалежно від існування користувача (не розкриває, хто зареєстрований).</summary>
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerification(EmailRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
        string? token = null;
        if (user is not null && !user.EmailConfirmed)
            token = await SendVerificationEmailAsync(user, ct);

        return Ok(new { message = "If the account exists and is not confirmed, a new email has been sent.",
                        devVerificationToken = env.IsDevelopment() ? token : null });
    }

    // ---------- Login / refresh / revoke / logout ----------

    /// <summary>Login: access token — у тілі відповіді, refresh token — у HttpOnly cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByNameAsync(request.UserNameOrEmail) ?? await users.FindByEmailAsync(request.UserNameOrEmail);
        if (user is null) return UnauthorizedProblem("Invalid credentials.");

        // непідтверджений email → login заблоковано
        if (!user.EmailConfirmed)
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Email is not confirmed.",
                detail: "Confirm the email (POST /auth/verify-email) or request a new message (POST /auth/resend-verification).");

        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return Problem(statusCode: StatusCodes.Status423Locked, title: "Account is locked after too many failed attempts.");
        if (!result.Succeeded) return UnauthorizedProblem("Invalid credentials.");

        var response = await IssueTokensAsync(user, ct);
        logger.LogInformation("User {UserId} logged in", user.Id);
        return Ok(response);
    }

    /// <summary>Новий access token за refresh-токеном (cookie або тіло). Старий refresh-токен відкликається, видається новий (rotation).</summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] TokenRequest? body, CancellationToken ct)
    {
        var token = Request.Cookies[RefreshCookie] ?? body?.RefreshToken;
        if (string.IsNullOrEmpty(token)) return UnauthorizedProblem("Refresh token is missing.");

        var (status, user, newToken) = await refreshTokens.RotateAsync(token, ClientIp(), ct);
        if (status != RefreshTokenService.RotationStatus.Ok)
        {
            Response.Cookies.Delete(RefreshCookie, CookieOptions(expires: null));
            return UnauthorizedProblem(status switch
            {
                RefreshTokenService.RotationStatus.Reused => "Refresh token was already used; all sessions have been revoked.",
                RefreshTokenService.RotationStatus.Expired => "Refresh token has expired.",
                _ => "Invalid refresh token."
            });
        }

        var roles = await users.GetRolesAsync(user!);
        var (access, expiresAt) = jwt.GenerateAccessToken(user!, roles);
        SetRefreshCookie(newToken!);
        return Ok(new AuthResponse(access, expiresAt));
    }

    /// <summary>Відкликання refresh-токена поточного користувача (cookie або тіло).</summary>
    [HttpPost("revoke-token")]
    [Authorize]
    public async Task<IActionResult> Revoke([FromBody] TokenRequest? body, CancellationToken ct)
    {
        var token = body?.RefreshToken ?? Request.Cookies[RefreshCookie];
        if (string.IsNullOrEmpty(token)) return BadRequestProblem("Refresh token is required.");

        var revoked = await refreshTokens.RevokeAsync(token, CurrentUserId(), "revoked_by_user", ct);
        if (!revoked) return NotFound(new ProblemDetails { Title = "Active refresh token not found." });

        return NoContent();
    }

    /// <summary>Logout: відкликає refresh-токен із cookie й видаляє cookie.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies[RefreshCookie] is { Length: > 0 } token)
            await refreshTokens.RevokeAsync(token, CurrentUserId(), "logout", ct);

        Response.Cookies.Delete(RefreshCookie, CookieOptions(expires: null));
        logger.LogInformation("User {UserId} logged out", CurrentUserId());
        return NoContent();
    }

    // ---------- Паролі ----------

    /// <summary>Запит на скидання пароля: лист із одноразовим токеном (mock). Відповідь однакова для будь-якого email.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
        string? token = null;
        if (user is not null && user.EmailConfirmed)
        {
            token = Encode(await users.GeneratePasswordResetTokenAsync(user));
            await email.SendAsync(user.Email!, "Password reset",
                $"POST /auth/reset-password {{ \"email\": \"{user.Email}\", \"token\": \"{token}\", \"newPassword\": \"...\" }}", ct);
        }

        return Ok(new { message = "If the account exists, a password reset email has been sent.",
                        devResetToken = env.IsDevelopment() ? token : null });
    }

    /// <summary>Скидання пароля за токеном. Токен одноразовий і має термін дії; після скидання відкликаються всі refresh-токени.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null) return BadRequestProblem("Invalid password reset request.");

        var result = await users.ResetPasswordAsync(user, Decode(request.Token), request.NewPassword);
        if (!result.Succeeded) return IdentityProblem(result);

        await refreshTokens.RevokeAllAsync(user.Id, "password_reset", ct);
        logger.LogInformation("Password reset for user {UserId}; all refresh tokens revoked", user.Id);
        return Ok(new { message = "Password has been reset." });
    }

    /// <summary>Зміна пароля (поточний + новий). Інші сесії завершуються — усі refresh-токени відкликаються.</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(CurrentUserId()!);
        if (user is null) return Unauthorized();

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return IdentityProblem(result);

        await refreshTokens.RevokeAllAsync(user.Id, "password_changed", ct);
        Response.Cookies.Delete(RefreshCookie, CookieOptions(expires: null));
        return NoContent();
    }

    // ---------- Допоміжні ----------

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = await users.GetRolesAsync(user);
        var (access, expiresAt) = jwt.GenerateAccessToken(user, roles);
        var refresh = await refreshTokens.IssueAsync(user, ClientIp(), ct);
        SetRefreshCookie(refresh);
        return new AuthResponse(access, expiresAt);
    }

    private async Task<string> SendVerificationEmailAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = Encode(await users.GenerateEmailConfirmationTokenAsync(user));
        await email.SendAsync(user.Email!, "Confirm your email",
            $"POST /auth/verify-email {{ \"email\": \"{user.Email}\", \"token\": \"{token}\" }}", ct);
        return token;
    }

    private void SetRefreshCookie(string token) =>
        Response.Cookies.Append(RefreshCookie, token, CookieOptions(DateTimeOffset.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays)));

    // HttpOnly: недоступний для JavaScript (захист від викрадення через XSS); Path=/auth: cookie не їде на інші маршрути
    private CookieOptions CookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = !env.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = "/auth",
        Expires = expires
    };

    private string? CurrentUserId() => User.FindFirstValue(JwtRegisteredClaimNames.Sub);
    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    // токени Identity містять символи +/=, небезпечні в URL → base64url
    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    private static string Decode(string token)
    {
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)); }
        catch (FormatException) { return token; }
    }

    private ObjectResult UnauthorizedProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: detail);

    private ObjectResult BadRequestProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad request", detail: detail);

    private IActionResult IdentityProblem(IdentityResult result)
    {
        var errors = result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return ValidationProblem(new ValidationProblemDetails(errors));
    }
}

public static class Roles
{
    public const string User = "User";
    public const string Admin = "Admin";
    public static readonly string[] All = [User, Admin];
}
