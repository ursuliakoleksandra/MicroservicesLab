using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Platform.ServiceDefaults.Correlation;
using Serilog.Context;

namespace Platform.ServiceDefaults;

/// <summary>
/// Наскрізний CorrelationId (Лаб. 05, ч. 4.2 / 5.2).
/// X-Correlation-Id береться з вхідного запиту або генерується (для запитів в обхід Gateway); потрапляє в LogContext
/// (а отже в усі логи запиту), у тег поточного Activity та в заголовок відповіді; межі запиту логуються.
/// CorrelationId — це бізнес-ідентифікатор звернення; TraceId генерує OpenTelemetry для технічного трасування.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;
        var generated = false;

        if (context.Request.Headers.TryGetValue(HeaderName, out var incoming) && IsValid(incoming.ToString()))
        {
            correlationId = incoming.ToString();
        }
        else
        {
            correlationId = Guid.NewGuid().ToString();
            generated = true;
            context.Request.Headers[HeaderName] = correlationId;   // далі його побачать YARP і HttpClient
        }

        // OnStarting, а не пряме присвоєння: ExceptionHandler очищає заголовки відповіді, а YARP копіює заголовки сервісу
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        CorrelationContext.Current = correlationId;                  // Лаб. 08: CorrelationId потрапляє в інтеграційні події
        Activity.Current?.SetTag("correlation.id", correlationId);   // пошук trace за CorrelationId у Dashboard

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            if (generated)
                logger.LogInformation("New CorrelationId {CorrelationId} generated for {Method} {Path}",
                    correlationId, context.Request.Method, context.Request.Path);

            logger.LogInformation("Request started {Method} {Path}", context.Request.Method, context.Request.Path);
            var started = Stopwatch.GetTimestamp();

            await next(context);

            logger.LogInformation("Request finished with {StatusCode} in {Elapsed} ms",
                context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds.ToString("F1"));
        }
    }

    /// <summary>Заголовок приходить ззовні, тому довжина й символи обмежені (захист від log injection).</summary>
    private static bool IsValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength) return false;
        foreach (var c in value)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':')) return false;
        return true;
    }
}

/// <summary>DelegatingHandler: додає CorrelationId поточного запиту у вихідні HTTP-виклики до інших сервісів.</summary>
public sealed class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var id = accessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        if (!string.IsNullOrEmpty(id))
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, id);

        return base.SendAsync(request, cancellationToken);
    }
}
