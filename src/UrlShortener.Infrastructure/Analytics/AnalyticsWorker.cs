using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Infrastructure.Analytics;

public sealed class AnalyticsWorker : BackgroundService
{
    private readonly IAnalyticsQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalyticsWorker> _logger;

    public AnalyticsWorker(
        IAnalyticsQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AnalyticsWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Analytics worker started");

        await foreach (var evt in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var clicks = scope.ServiceProvider.GetRequiredService<IClickEventRepository>();
                var urls = scope.ServiceProvider.GetRequiredService<IUrlRepository>();

                var entity = ClickEvent.Create(
                    evt.ShortCode,
                    evt.OccurredAt,
                    evt.Referrer,
                    evt.UserAgent,
                    evt.DeviceCategory,
                    evt.Country);

                await clicks.AddAsync(entity, stoppingToken);

                var url = await urls.GetByShortCodeAsync(evt.ShortCode, stoppingToken);
                url?.IncrementClickCount();

                await clicks.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to persist analytics for {ShortCode}", evt.ShortCode);
            }
        }
    }
}
