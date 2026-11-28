using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Platform.Security.Authentication;

/// <summary>
/// Автентифікація кількох емітентів токенів на одному ресурс-сервері. Платформа реалізує три підходи
/// (Лаб. 09), тому Gateway і мікросервіси приймають токени будь-якого з них:
/// <list type="bullet">
/// <item><b>Local</b> — власний Auth Service (ASP.NET Identity + JWT, симетричний ключ, Частина 1);</item>
/// <item><b>IdentityServer</b> — Duende IdentityServer (OAuth 2.0; ключі з discovery-документа Authority, Частина 2);</item>
/// <item><b>Keycloak</b> — realm TechNestRealm (OIDC discovery, Частина 3).</item>
/// </list>
/// Схема «Smart» читає claim <c>iss</c> з токена й делегує перевірку відповідній схемі JwtBearer.
/// Кожна схема вмикається лише якщо задано її конфігурацію (Jwt:SecretKey, IdentityServer:Authority, Keycloak:Authority).
/// </summary>
public static class AuthenticationExtensions
{
    public const string SmartScheme = "Smart";
    public const string LocalScheme = "Local";
    public const string IdentityServerScheme = "IdentityServer";
    public const string KeycloakScheme = "Keycloak";

    /// <param name="identityServerAudience">Ім'я ApiResource цього сервісу (audience токенів IdentityServer); null — не перевіряти.</param>
    public static AuthenticationBuilder AddPlatformAuthentication(this WebApplicationBuilder builder, string? identityServerAudience = null)
    {
        var config = builder.Configuration;
        var isDevelopment = builder.Environment.IsDevelopment();

        var jwt = config.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
        var identityServerAuthority = config["IdentityServer:Authority"]?.TrimEnd('/');
        var keycloakAuthority = config["Keycloak:Authority"]?.TrimEnd('/');

        builder.Services.Configure<JwtSettings>(config.GetSection(JwtSettings.SectionName));

        var auth = builder.Services.AddAuthentication(o =>
        {
            o.DefaultScheme = SmartScheme;
            o.DefaultAuthenticateScheme = SmartScheme;
            o.DefaultChallengeScheme = SmartScheme;
            o.DefaultForbidScheme = SmartScheme;
        });

        auth.AddPolicyScheme(SmartScheme, "Local / IdentityServer / Keycloak", o =>
        {
            o.ForwardDefaultSelector = ctx => SelectScheme(ctx, jwt, identityServerAuthority, keycloakAuthority);
        });

        if (jwt.IsConfigured)
        {
            if (jwt.SecretKey.Length < 32)
                throw new InvalidOperationException("Jwt:SecretKey must be at least 32 characters long.");

            auth.AddJwtBearer(LocalScheme, o =>
            {
                o.MapInboundClaims = false;   // claims лишаються з іменами з токена: sub, unique_name, role
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "unique_name",
                    RoleClaimType = "role"
                };
            });
        }

        if (!string.IsNullOrWhiteSpace(identityServerAuthority))
        {
            auth.AddJwtBearer(IdentityServerScheme, o =>
            {
                o.Authority = identityServerAuthority;               // ключі підпису — з /.well-known/openid-configuration
                o.RequireHttpsMetadata = !isDevelopment;
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = identityServerAudience is not null,
                    ValidAudience = identityServerAudience,         // ApiResource цього сервісу
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });
        }

        if (!string.IsNullOrWhiteSpace(keycloakAuthority))
        {
            auth.AddJwtBearer(KeycloakScheme, o =>
            {
                o.Authority = keycloakAuthority;                     // http://localhost:8080/realms/TechNestRealm
                o.RequireHttpsMetadata = !isDevelopment;             // Keycloak у розробці — HTTP
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    // Один токен користувача містить permissions для всіх ресурс-серверів, тому audience не розрізняє
                    // сервіси; рішення приймає PermissionAuthorizationHandler за claim authorization.permissions.
                    ValidateAudience = false,
                    NameClaimType = "preferred_username"
                };
            });
        }

        return auth;
    }

    private static string SelectScheme(HttpContext ctx, JwtSettings jwt, string? identityServer, string? keycloak)
    {
        var fallback = jwt.IsConfigured ? LocalScheme
            : !string.IsNullOrWhiteSpace(identityServer) ? IdentityServerScheme
            : KeycloakScheme;

        var header = ctx.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return fallback;

        try
        {
            var issuer = new JsonWebTokenHandler().ReadJsonWebToken(header["Bearer ".Length..].Trim()).Issuer?.TrimEnd('/');
            if (string.IsNullOrEmpty(issuer)) return fallback;

            if (jwt.IsConfigured && issuer == jwt.Issuer) return LocalScheme;
            if (!string.IsNullOrWhiteSpace(identityServer) && issuer == identityServer) return IdentityServerScheme;
            if (!string.IsNullOrWhiteSpace(keycloak) && (issuer == keycloak || issuer.Contains("/realms/"))) return KeycloakScheme;
        }
        catch (Exception)
        {
            // не JWT — нехай перевірить схема за замовчуванням (поверне 401)
        }

        return fallback;
    }
}
