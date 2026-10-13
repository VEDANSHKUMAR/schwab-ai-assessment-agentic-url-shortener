using UrlShortener.Domain.Entities;

namespace UrlShortener.Application.Abstractions;

public interface IUrlRepository
{
    Task<ShortUrl?> GetByShortCodeAsync(string shortCode, CancellationToken cancellationToken);
    Task<ShortUrl?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string shortCode, CancellationToken cancellationToken);
    Task AddAsync(ShortUrl shortUrl, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
