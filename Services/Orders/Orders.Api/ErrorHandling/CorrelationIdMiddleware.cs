using Serilog.Context;

namespace Orders.Api.ErrorHandling;

/// <summary>Кореляційний ідентифікатор: береться із заголовка X-Correlation-ID або генерується; потрапляє в усі логи запиту.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var values) && !string.IsNullOrWhiteSpace(values)
            ? values.ToString()
            : context.TraceIdentifier;

        context.Response.Headers[HeaderName] = correlationId;
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
