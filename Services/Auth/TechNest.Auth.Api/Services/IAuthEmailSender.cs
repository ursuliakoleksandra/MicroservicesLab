namespace TechNest.Auth.Api.Services;

public interface IAuthEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}

/// <summary>
/// Mock email-сервіс для розробки: листи не відправляються, а виводяться в лог (Dashboard → Structured logs).
/// У production замінюється реалізацією на SMTP/SendGrid без змін в інших місцях.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IAuthEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        logger.LogInformation("MOCK EMAIL to {Email} | {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
