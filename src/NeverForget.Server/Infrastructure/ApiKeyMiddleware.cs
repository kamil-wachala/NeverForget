using Microsoft.Extensions.Primitives;

namespace NeverForget.Server.Infrastructure;

public sealed class ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private const string HeaderName = "X-Api-Key";
    private readonly string? _configuredApiKey = configuration["ApiKey"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_configuredApiKey)
            || context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out StringValues suppliedKey)
            || !string.Equals(suppliedKey.ToString(), _configuredApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = $"Missing or invalid {HeaderName} header." });
            return;
        }

        await next(context);
    }
}
