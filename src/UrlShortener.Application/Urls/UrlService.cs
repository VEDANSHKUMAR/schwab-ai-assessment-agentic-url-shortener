using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Options;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Exceptions;
using UrlShortener.Domain.Services;

namespace UrlShortener.Application.Urls;

public interface IUrlService
{
    Task<CreateShortUrlResponse> CreateAsync(
        CreateShortUrlRequest request,
        string? apiKeyId,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    Task<ShortUrlDetailsResponse> GetDetailsAsync(string shortCode, CancellationToken cancellationToken);

    Task DisableAsync(string shortCode, CancellationToken cancellationToken);

    Task<string> ResolveForRedirectAsync(string shortCode, CancellationToken cancellationToken);

    Task<AnalyticsResponse> GetAnalyticsAsync(string shortCode, CancellationToken cancellationToken);
}

public sealed class UrlService : IUrlService
{
    private readonly IUrlRepository _urls;
    private readonly IClickEventRepository _clicks;
    private readonly IUrlCache _cache;
    private readonly IShortCodeGenerator _codes;
    private readonly IValidator<CreateShortUrlRequest> _validator;
    private readonly IOptions<UrlShortenerOptions> _options;
    private readonly ILogger<UrlService> _logger;

    public UrlService(
        IUrlRepository urls,
        IClickEventRepository clicks,
        IUrlCache cache,
        IShortCodeGenerator codes,
        IValidator<CreateShortUrlRequest> validator,
        IOptions<UrlShortenerOptions> options,
        ILogger<UrlService> logger)
    {
        _urls = urls;
        _clicks = clicks;
        _cache = cache;
        _codes = codes;
        _validator = validator;
        _options = options;
        _logger = logger;
    }

    public async Task<CreateShortUrlResponse> CreateAsync(
        CreateShortUrlRequest request,
        string? apiKeyId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _urls.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return ToCreateResponse(existing);
            }
        }

        var now = DateTimeOffset.UtcNow;
        string shortCode;

        if (!string.IsNullOrWhiteSpace(request.CustomAlias))
        {
            shortCode = request.CustomAlias;
            if (await _urls.ExistsAsync(shortCode, cancellationToken))
            {
                throw new ConflictException($"Custom alias '{shortCode}' is already in use.");
            }
        }
        else
        {
            shortCode = await GenerateUniqueCodeAsync(cancellationToken);
        }

        var entity = ShortUrl.Create(
            shortCode,
            request.Url.Trim(),
            now,
            request.ExpiresAt,
            apiKeyId,
            string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey);

        try
        {
            await _urls.AddAsync(entity, cancellationToken);
            await _urls.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            if (!string.IsNullOrWhiteSpace(request.CustomAlias))
            {
                throw new ConflictException($"Custom alias '{shortCode}' is already in use.");
            }

            throw new ConflictException("Short-code collision occurred; please retry.");
        }

        _logger.LogInformation("Created short URL {ShortCode}", entity.ShortCode);
        return ToCreateResponse(entity);
    }

    public async Task<ShortUrlDetailsResponse> GetDetailsAsync(string shortCode, CancellationToken cancellationToken)
    {
        EnsureValidCode(shortCode);
        var entity = await _urls.GetByShortCodeAsync(shortCode, cancellationToken)
                     ?? throw new NotFoundException($"Short URL '{shortCode}' was not found.");

        return new ShortUrlDetailsResponse(
            entity.ShortCode,
            entity.OriginalUrl,
            BuildShortUrl(entity.ShortCode),
            entity.CreatedAt,
            entity.ExpiresAt,
            entity.IsActive,
            entity.ClickCount);
    }

    public async Task DisableAsync(string shortCode, CancellationToken cancellationToken)
    {
        EnsureValidCode(shortCode);
        var entity = await _urls.GetByShortCodeAsync(shortCode, cancellationToken)
                     ?? throw new NotFoundException($"Short URL '{shortCode}' was not found.");

        entity.Disable(DateTimeOffset.UtcNow);
        await _urls.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(shortCode, cancellationToken);
        _logger.LogInformation("Disabled short URL {ShortCode}", shortCode);
    }

    public async Task<string> ResolveForRedirectAsync(string shortCode, CancellationToken cancellationToken)
    {
        EnsureValidCode(shortCode);
        var now = DateTimeOffset.UtcNow;

        CachedUrl? cached = null;
        try
        {
            cached = await _cache.GetAsync(shortCode, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis read failed for {ShortCode}; falling back to PostgreSQL", shortCode);
        }

        if (cached is not null)
        {
            return ValidateCached(cached, now);
        }

        var entity = await _urls.GetByShortCodeAsync(shortCode, cancellationToken)
                     ?? throw new NotFoundException($"Short URL '{shortCode}' was not found.");

        if (!entity.IsActive)
        {
            throw new GoneException($"Short URL '{shortCode}' has been disabled.");
        }

        if (entity.IsExpired(now))
        {
            throw new GoneException($"Short URL '{shortCode}' has expired.");
        }

        var cacheValue = new CachedUrl(entity.ShortCode, entity.OriginalUrl, entity.IsActive, entity.ExpiresAt);
        try
        {
            var ttl = ResolveTtl(entity.ExpiresAt, now);
            await _cache.SetAsync(cacheValue, ttl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis write failed for {ShortCode}", shortCode);
        }

        return entity.OriginalUrl;
    }

    public async Task<AnalyticsResponse> GetAnalyticsAsync(string shortCode, CancellationToken cancellationToken)
    {
        EnsureValidCode(shortCode);
        _ = await _urls.GetByShortCodeAsync(shortCode, cancellationToken)
            ?? throw new NotFoundException($"Short URL '{shortCode}' was not found.");

        var to = DateTimeOffset.UtcNow;
        var from = to.AddDays(-30);
        var total = await _clicks.GetTotalClicksAsync(shortCode, cancellationToken);
        var byDay = await _clicks.GetClicksByDayAsync(shortCode, from, to, cancellationToken);
        var recent = await _clicks.GetRecentAsync(shortCode, 50, cancellationToken);

        return new AnalyticsResponse(
            shortCode,
            total,
            byDay.Select(x => new ClicksByDayItem(x.Day, x.Count)).ToList(),
            recent.Select(c => new RecentClickItem(
                c.OccurredAt,
                c.Referrer,
                c.UserAgent,
                c.DeviceCategory,
                c.Country)).ToList());
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        var length = _options.Value.ShortCodeLength;
        var retries = _options.Value.MaxCollisionRetries;

        for (var i = 0; i < retries; i++)
        {
            var code = _codes.Generate(length);
            if (!await _urls.ExistsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw new ConflictException("Unable to allocate a unique short code; please retry.");
    }

    private CreateShortUrlResponse ToCreateResponse(ShortUrl entity) =>
        new(entity.ShortCode, BuildShortUrl(entity.ShortCode), entity.OriginalUrl, entity.CreatedAt, entity.ExpiresAt);

    private string BuildShortUrl(string shortCode) =>
        $"{_options.Value.BaseUrl.TrimEnd('/')}/{shortCode}";

    private static void EnsureValidCode(string shortCode)
    {
        if (!ShortCodeGenerator.IsValidFormat(shortCode))
        {
            throw new ValidationDomainException("Invalid short code format.");
        }
    }

    private static string ValidateCached(CachedUrl cached, DateTimeOffset now)
    {
        if (!cached.IsActive)
        {
            throw new GoneException($"Short URL '{cached.ShortCode}' has been disabled.");
        }

        if (cached.ExpiresAt is not null && cached.ExpiresAt <= now)
        {
            throw new GoneException($"Short URL '{cached.ShortCode}' has expired.");
        }

        return cached.OriginalUrl;
    }

    private TimeSpan? ResolveTtl(DateTimeOffset? expiresAt, DateTimeOffset now)
    {
        var defaultTtl = TimeSpan.FromSeconds(_options.Value.CacheTtlSeconds);
        if (expiresAt is null)
        {
            return defaultTtl;
        }

        var untilExpiry = expiresAt.Value - now;
        if (untilExpiry <= TimeSpan.Zero)
        {
            return TimeSpan.FromSeconds(1);
        }

        return untilExpiry < defaultTtl ? untilExpiry : defaultTtl;
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("23505", StringComparison.Ordinal)
                || message.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
