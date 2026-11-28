using System.Diagnostics;
using Platform.Security.Authentication;
using Platform.ServiceDefaults;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();      // Serilog, OpenTelemetry, health checks, service discovery

// Лаб. 09: Gateway перевіряє JWT усіх трьох провайдерів (перший рубіж); сервіси перевіряють токен повторно
builder.AddPlatformAuthentication();
builder.Services.AddAuthorizationBuilder()   // політики для ReverseProxy:Routes:*:AuthorizationPolicy ("anonymous" — вбудована в YARP)
    .AddPolicy("authenticated", p => p.RequireAuthenticatedUser());

// маршрути й кластери — в appsettings.json (секція ReverseProxy); адреси http://<ім'я-ресурсу> розв'язує service discovery Aspire
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(ctx =>
    {
        // X-User-Id: sub із перевіреного токена (для логів/аудиту downstream; клієнтський заголовок затирається).
        // Заголовок Authorization YARP передає далі без змін — сервіси перевіряють той самий токен.
        ctx.AddRequestTransform(t =>
        {
            t.ProxyRequest.Headers.Remove("X-User-Id");
            var sub = t.HttpContext.User.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(sub)) t.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", sub);
            return ValueTask.CompletedTask;
        });
    })
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();       // ДО MapReverseProxy: ідентифікатор потрапляє в проксований запит

// логування рішень маршрутизації й показників: метод, шлях, маршрут, статус, тривалість, розмір, помилки сервісів
app.Use(async (context, next) =>
{
    var started = Stopwatch.GetTimestamp();
    await next();

    var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    var proxy = context.GetReverseProxyFeature();
    var route = proxy?.Route?.Config.RouteId ?? "(none)";
    var cluster = proxy?.Cluster?.Model.Config.ClusterId ?? "(none)";

    app.Logger.LogInformation(
        "{Method} {Path} from {ClientIp} -> route {Route} / cluster {Cluster}: {StatusCode} in {Elapsed} ms, {ResponseBytes} bytes",
        context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress, route, cluster,
        context.Response.StatusCode, elapsedMs.ToString("F1"), context.Response.ContentLength);

    // недоступний сервіс: connection refused, timeout тощо
    var error = context.Features.Get<IForwarderErrorFeature>();
    if (error is not null)
        app.Logger.LogWarning(error.Exception,
            "Forwarding to cluster {Cluster} failed: {ForwarderError} (route {Route}, status {StatusCode})",
            cluster, error.Error, route, context.Response.StatusCode);
});

app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
app.MapDefaultEndpoints();         // /health і /alive
app.Run();
