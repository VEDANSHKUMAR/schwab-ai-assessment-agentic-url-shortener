using Microsoft.Extensions.Options;
using UrlShortener.Application.Options;

namespace UrlShortener.Api.Security;

public sealed class ApiKeyMiddleware
{
    public const string HeaderName = "X-Api-Key";
    public const string ItemKey = "ApiKeyId";

    private readonly RequestDelegate _next;

    public ApiKeyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IOptions<UrlShortenerOptions> options)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        var requiresKey =
            path.StartsWith("/api/v1/urls", StringComparison.OrdinalIgnoreCase)
            && (HttpMethods.IsPost(context.Request.Method)
                || HttpMethods.IsDelete(context.Request.Method)
                || (HttpMethods.IsGet(context.Request.Method) && path.Contains("/analytics", StringComparison.OrdinalIgnoreCase)));

        // GET details also protected lightly — require key for management reads
        if (HttpMethods.IsGet(context.Request.Method)
            && path.StartsWith("/api/v1/urls/", StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith("/analytics", StringComparison.OrdinalIgnoreCase))
        {
            requiresKey = true;
        }

        if (!requiresKey)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var key) || string.IsNullOrWhiteSpace(key))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.com/401",
                title = "Unauthorized",
                status = 401,
                detail = $"Missing {HeaderName} header."
            });
            return;
        }

        var configured = options.Value.ApiKeys;
        if (!configured.Contains(key.ToString(), StringComparer.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.com/403",
                title = "Forbidden",
                status = 403,
                detail = "Invalid API key."
            });
            return;
        }

        context.Items[ItemKey] = key.ToString();
        await _next(context);
    }
}
