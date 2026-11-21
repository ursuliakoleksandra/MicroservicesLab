using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.ServiceDefaults;
using Xunit;

namespace Platform.Tests;

public class CorrelationIdMiddlewareTests
{
    private static async Task<(DefaultHttpContext Context, bool NextCalled)> RunAsync(string? incomingHeader)
    {
        var context = new DefaultHttpContext();
        if (incomingHeader is not null) context.Request.Headers[CorrelationIdMiddleware.HeaderName] = incomingHeader;

        var nextCalled = false;
        var middleware = new CorrelationIdMiddleware(_ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        return (context, nextCalled);
    }

    [Fact]
    public async Task WithoutHeader_GeneratesGuid_AndPutsItInRequest_ForYarpAndHttpClient()
    {
        var (context, nextCalled) = await RunAsync(null);

        Assert.True(nextCalled);
        Assert.True(Guid.TryParse(context.Request.Headers[CorrelationIdMiddleware.HeaderName], out _));
    }

    [Fact]
    public async Task WithHeader_KeepsClientValue()
    {
        var (context, _) = await RunAsync("order-123-abc");

        Assert.Equal("order-123-abc", context.Request.Headers[CorrelationIdMiddleware.HeaderName]);
    }

    [Theory]
    [InlineData("bad value with spaces")]
    [InlineData("line\nbreak")]
    public async Task WithSuspiciousHeader_ReplacesItWithGuid(string header)
    {
        var (context, _) = await RunAsync(header);

        Assert.True(Guid.TryParse(context.Request.Headers[CorrelationIdMiddleware.HeaderName], out _));
    }
}
