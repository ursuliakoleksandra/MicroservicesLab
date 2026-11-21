using Serilog.Context;

namespace Reviews.WebApi.ErrorHandling;

/// <summary>Кореляційний ідентифікатор (X-Correlation-ID або TraceIdentifier) потрапляє в усі логи запиту, зокрема LoggingBehavior.</summary>
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
