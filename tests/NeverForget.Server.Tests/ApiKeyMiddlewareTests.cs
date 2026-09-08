using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NeverForget.Server.Infrastructure;

namespace NeverForget.Server.Tests;

public sealed class ApiKeyMiddlewareTests
{
    [Fact]
    public async Task Configured_key_rejects_request_without_header()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware("secret", _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Configured_key_accepts_matching_header()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware("secret", _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "secret";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    private static ApiKeyMiddleware CreateMiddleware(string apiKey, RequestDelegate next)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ApiKey"] = apiKey })
            .Build();
        return new ApiKeyMiddleware(next, configuration);
    }
}
