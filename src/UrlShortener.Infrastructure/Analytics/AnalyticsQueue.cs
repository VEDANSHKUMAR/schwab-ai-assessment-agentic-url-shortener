using System.Threading.Channels;
using UrlShortener.Application.Abstractions;

namespace UrlShortener.Infrastructure.Analytics;

public interface IAnalyticsQueue
{
    ValueTask EnqueueAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken);
    IAsyncEnumerable<AnalyticsEvent> DequeueAllAsync(CancellationToken cancellationToken);
    int ApproximateCount { get; }
}

public sealed class AnalyticsQueue : IAnalyticsQueue, IAnalyticsPublisher
{
    private readonly Channel<AnalyticsEvent> _channel =
        Channel.CreateBounded<AnalyticsEvent>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public int ApproximateCount => _channel.Reader.Count;

    public ValueTask PublishAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken) =>
        EnqueueAsync(analyticsEvent, cancellationToken);

    public ValueTask EnqueueAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(analyticsEvent, cancellationToken);

    public IAsyncEnumerable<AnalyticsEvent> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
