namespace UrlShortener.Domain.Entities;

public sealed class ShortUrl
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ShortCode { get; private set; } = null!;
    public string OriginalUrl { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? ApiKeyId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public long ClickCount { get; private set; }

    private ShortUrl()
    {
    }

    public static ShortUrl Create(
        string shortCode,
        string originalUrl,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt = null,
        string? apiKeyId = null,
        string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(shortCode))
        {
            throw new ArgumentException("Short code is required.", nameof(shortCode));
        }

        if (string.IsNullOrWhiteSpace(originalUrl))
        {
            throw new ArgumentException("Original URL is required.", nameof(originalUrl));
        }

        if (expiresAt is not null && expiresAt <= createdAt)
        {
            throw new ArgumentException("Expiration must be in the future.", nameof(expiresAt));
        }

        return new ShortUrl
        {
            ShortCode = shortCode,
            OriginalUrl = originalUrl,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            ExpiresAt = expiresAt,
            ApiKeyId = apiKeyId,
            IdempotencyKey = idempotencyKey,
            IsActive = true
        };
    }

    public bool IsExpired(DateTimeOffset utcNow) =>
        ExpiresAt is not null && ExpiresAt <= utcNow;

    public bool CanRedirect(DateTimeOffset utcNow) =>
        IsActive && !IsExpired(utcNow);

    public void Disable(DateTimeOffset utcNow)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        UpdatedAt = utcNow;
    }

    public void IncrementClickCount() => ClickCount++;
}
