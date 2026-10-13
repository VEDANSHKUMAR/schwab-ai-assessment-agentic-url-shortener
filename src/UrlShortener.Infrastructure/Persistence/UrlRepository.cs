using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Infrastructure.Persistence;

public sealed class UrlRepository : IUrlRepository
{
    private readonly AppDbContext _db;

    public UrlRepository(AppDbContext db) => _db = db;

    public Task<ShortUrl?> GetByShortCodeAsync(string shortCode, CancellationToken cancellationToken) =>
        _db.ShortUrls.FirstOrDefaultAsync(x => x.ShortCode == shortCode, cancellationToken);

    public Task<ShortUrl?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        _db.ShortUrls.FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<bool> ExistsAsync(string shortCode, CancellationToken cancellationToken) =>
        _db.ShortUrls.AnyAsync(x => x.ShortCode == shortCode, cancellationToken);

    public async Task AddAsync(ShortUrl shortUrl, CancellationToken cancellationToken) =>
        await _db.ShortUrls.AddAsync(shortUrl, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
