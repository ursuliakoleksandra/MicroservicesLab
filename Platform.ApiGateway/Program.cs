using System.Diagnostics;
using Platform.ServiceDefaults;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();      // Serilog, OpenTelemetry, health checks, service discovery

// маршрути й кластери — в appsettings.json (секція ReverseProxy); адреси http://<ім'я-ресурсу> розв'язує service discovery Aspire
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
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

app.MapReverseProxy();
app.MapDefaultEndpoints();         // /health і /alive
app.Run();
