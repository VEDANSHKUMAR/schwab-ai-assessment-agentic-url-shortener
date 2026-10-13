using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Urls;

namespace UrlShortener.Api.Controllers;

[ApiController]
public sealed class RedirectController : ControllerBase
{
    private readonly IUrlService _urls;
    private readonly IAnalyticsPublisher _analytics;
    private readonly ILogger<RedirectController> _logger;

    public RedirectController(
        IUrlService urls,
        IAnalyticsPublisher analytics,
        ILogger<RedirectController> logger)
    {
        _urls = urls;
        _analytics = analytics;
        _logger = logger;
    }

    [HttpGet("/{shortCode}")]
    [HttpHead("/{shortCode}")]
    [EnableRateLimiting("redirect")]
    public async Task<IActionResult> RedirectToOriginal(string shortCode, CancellationToken cancellationToken)
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "api", "health", "ready", "swagger", "orchestration", "index.html", "assets"
        };

        if (reserved.Contains(shortCode) || shortCode.Contains('.'))
        {
            return NotFound();
        }

        var destination = await _urls.ResolveForRedirectAsync(shortCode, cancellationToken);

        var referrer = Request.Headers.Referer.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var device = ClassifyDevice(ua);

        try
        {
            await _analytics.PublishAsync(
                new AnalyticsEvent(shortCode, DateTimeOffset.UtcNow, referrer, ua, device, null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue analytics for {ShortCode}", shortCode);
        }

        return Redirect(destination);
    }

    private static string ClassifyDevice(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return "unknown";
        }

        var ua = userAgent.ToLowerInvariant();
        if (ua.Contains("mobile") || ua.Contains("android") || ua.Contains("iphone"))
        {
            return "mobile";
        }

        if (ua.Contains("ipad") || ua.Contains("tablet"))
        {
            return "tablet";
        }

        return "desktop";
    }
}
