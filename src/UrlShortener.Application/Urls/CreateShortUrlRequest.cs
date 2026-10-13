namespace UrlShortener.Application.Urls;

public sealed record CreateShortUrlRequest(
    string Url,
    string? CustomAlias,
    DateTimeOffset? ExpiresAt);

public sealed record CreateShortUrlResponse(
    string ShortCode,
    string ShortUrl,
    string OriginalUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt);

public sealed record ShortUrlDetailsResponse(
    string ShortCode,
    string OriginalUrl,
    string ShortUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool IsActive,
    long ClickCount);

public sealed record AnalyticsResponse(
    string ShortCode,
    long TotalClicks,
    IReadOnlyList<ClicksByDayItem> ClicksOverTime,
    IReadOnlyList<RecentClickItem> RecentClicks);

public sealed record ClicksByDayItem(DateOnly Day, long Count);

public sealed record RecentClickItem(
    DateTimeOffset OccurredAt,
    string? Referrer,
    string? UserAgent,
    string? DeviceCategory,
    string? Country);
