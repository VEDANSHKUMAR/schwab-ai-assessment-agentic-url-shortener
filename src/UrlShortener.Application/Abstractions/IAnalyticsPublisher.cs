namespace UrlShortener.Application.Abstractions;

public sealed record AnalyticsEvent(
    string ShortCode,
    DateTimeOffset OccurredAt,
    string? Referrer,
    string? UserAgent,
    string? DeviceCategory,
    string? Country);

public interface IAnalyticsPublisher
{
    ValueTask PublishAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken);
}
