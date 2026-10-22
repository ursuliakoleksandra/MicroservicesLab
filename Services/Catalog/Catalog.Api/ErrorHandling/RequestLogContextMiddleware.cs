using Serilog.Context;

namespace Catalog.Api.ErrorHandling;

/// <summary>Додає до всіх логів запиту властивості CorrelationId, RequestPath та UserId (якщо користувач автентифікований).</summary>
public sealed class RequestLogContextMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var values) && !string.IsNullOrWhiteSpace(values)
            ? values.ToString()
            : context.TraceIdentifier;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value))
        using (LogContext.PushProperty("UserId", context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null))
        {
            await next(context);
        }
    }
}
