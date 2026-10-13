namespace UrlShortener.Domain.Entities;

public sealed class ClickEvent
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ShortCode { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public string? Referrer { get; private set; }
    public string? UserAgent { get; private set; }
    public string? DeviceCategory { get; private set; }
    public string? Country { get; private set; }

    private ClickEvent()
    {
    }

    public static ClickEvent Create(
        string shortCode,
        DateTimeOffset occurredAt,
        string? referrer,
        string? userAgent,
        string? deviceCategory,
        string? country = null)
    {
        return new ClickEvent
        {
            ShortCode = shortCode,
            OccurredAt = occurredAt,
            Referrer = Truncate(referrer, 512),
            UserAgent = Truncate(userAgent, 512),
            DeviceCategory = deviceCategory,
            Country = country
        };
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}
