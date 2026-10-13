using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using UrlShortener.Application.Abstractions;

namespace UrlShortener.Infrastructure.Caching;

public sealed class RedisUrlCache : IUrlCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisUrlCache> _logger;

    public RedisUrlCache(IConnectionMultiplexer redis, ILogger<RedisUrlCache> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private static string Key(string shortCode) => $"url:{shortCode}";

    public async Task<CachedUrl?> GetAsync(string shortCode, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(Key(shortCode));
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<CachedUrl>((string)value!, JsonOptions);
    }

    public async Task SetAsync(CachedUrl value, TimeSpan? ttl, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await db.StringSetAsync(Key(value.ShortCode), json, ttl);
    }

    public async Task RemoveAsync(string shortCode, CancellationToken cancellationToken)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(Key(shortCode));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache for {ShortCode}", shortCode);
        }
    }
}
