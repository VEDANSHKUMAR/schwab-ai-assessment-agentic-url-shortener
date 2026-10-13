namespace UrlShortener.Application.Options;

public sealed class UrlShortenerOptions
{
    public const string SectionName = "UrlShortener";

    public string BaseUrl { get; set; } = "http://localhost:5488";
    public int ShortCodeLength { get; set; } = 8;
    public int MaxCollisionRetries { get; set; } = 5;
    public int CacheTtlSeconds { get; set; } = 3600;
    public string[] BlockedDomains { get; set; } = [];
    public string[] ApiKeys { get; set; } = ["dev-api-key-change-me"];
    public int CreateRateLimitPerMinute { get; set; } = 60;
    public int RedirectRateLimitPerMinute { get; set; } = 600;
}
