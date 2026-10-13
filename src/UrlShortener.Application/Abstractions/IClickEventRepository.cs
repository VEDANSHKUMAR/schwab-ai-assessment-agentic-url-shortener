using UrlShortener.Domain.Entities;

namespace UrlShortener.Application.Abstractions;

public interface IClickEventRepository
{
    Task AddAsync(ClickEvent clickEvent, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<long> GetTotalClicksAsync(string shortCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClickEvent>> GetRecentAsync(string shortCode, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<(DateOnly Day, long Count)>> GetClicksByDayAsync(
        string shortCode,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}
