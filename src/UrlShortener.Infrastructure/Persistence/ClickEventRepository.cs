using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Infrastructure.Persistence;

public sealed class ClickEventRepository : IClickEventRepository
{
    private readonly AppDbContext _db;

    public ClickEventRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(ClickEvent clickEvent, CancellationToken cancellationToken) =>
        await _db.ClickEvents.AddAsync(clickEvent, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);

    public Task<long> GetTotalClicksAsync(string shortCode, CancellationToken cancellationToken) =>
        _db.ClickEvents.LongCountAsync(x => x.ShortCode == shortCode, cancellationToken);

    public async Task<IReadOnlyList<ClickEvent>> GetRecentAsync(
        string shortCode,
        int take,
        CancellationToken cancellationToken) =>
        await _db.ClickEvents
            .Where(x => x.ShortCode == shortCode)
            .OrderByDescending(x => x.OccurredAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<(DateOnly Day, long Count)>> GetClicksByDayAsync(
        string shortCode,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var rows = await _db.ClickEvents
            .Where(x => x.ShortCode == shortCode && x.OccurredAt >= from && x.OccurredAt <= to)
            .GroupBy(x => DateOnly.FromDateTime(x.OccurredAt.UtcDateTime))
            .Select(g => new { Day = g.Key, Count = g.LongCount() })
            .OrderBy(x => x.Day)
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.Day, r.Count)).ToList();
    }
}
