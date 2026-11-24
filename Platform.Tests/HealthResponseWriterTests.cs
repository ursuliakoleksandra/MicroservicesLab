using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.ServiceDefaults;
using Xunit;

namespace Platform.Tests;

public class HealthResponseWriterTests
{
    private static async Task<JsonDocument> WriteAsync(bool details)
    {
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>
        {
            ["orders-db"] = new(HealthStatus.Healthy, "ok", TimeSpan.FromMilliseconds(4), null, null, ["database", "ready"]),
            ["redis-cache"] = new(HealthStatus.Degraded, "Redis is down", TimeSpan.FromMilliseconds(3000), null,
                new Dictionary<string, object> { ["l2.available"] = false }, ["cache", "ready"])
        }, TimeSpan.FromMilliseconds(3004));

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await HealthResponseWriter.Create(details)(context, report);

        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    [Fact]
    public async Task Development_ShowsStatusAndDetailsOfEveryCheck()
    {
        using var json = await WriteAsync(details: true);
        var root = json.RootElement;

        Assert.Equal("Degraded", root.GetProperty("status").GetString());
        Assert.Equal("Healthy", root.GetProperty("entries").GetProperty("orders-db").GetProperty("status").GetString());
        Assert.Equal("Degraded", root.GetProperty("entries").GetProperty("redis-cache").GetProperty("status").GetString());
        Assert.False(root.GetProperty("entries").GetProperty("redis-cache").GetProperty("data").GetProperty("l2.available").GetBoolean());
    }

    [Fact]
    public async Task Production_HidesInternalDependencyNames()
    {
        using var json = await WriteAsync(details: false);

        Assert.Equal("Degraded", json.RootElement.GetProperty("status").GetString());
        Assert.False(json.RootElement.TryGetProperty("entries", out _));
    }
}
