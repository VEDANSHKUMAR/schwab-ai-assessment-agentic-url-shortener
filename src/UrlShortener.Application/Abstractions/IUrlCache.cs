namespace UrlShortener.Application.Abstractions;

public sealed record CachedUrl(
    string ShortCode,
    string OriginalUrl,
    bool IsActive,
    DateTimeOffset? ExpiresAt);

public interface IUrlCache
{
    Task<CachedUrl?> GetAsync(string shortCode, CancellationToken cancellationToken);
    Task SetAsync(CachedUrl value, TimeSpan? ttl, CancellationToken cancellationToken);
    Task RemoveAsync(string shortCode, CancellationToken cancellationToken);
}
