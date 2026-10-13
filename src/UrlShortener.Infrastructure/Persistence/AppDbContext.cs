using Microsoft.EntityFrameworkCore;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ShortUrl> ShortUrls => Set<ShortUrl>();
    public DbSet<ClickEvent> ClickEvents => Set<ClickEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShortUrl>(entity =>
        {
            entity.ToTable("urls");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ShortCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.OriginalUrl).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.ApiKeyId).HasMaxLength(128);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(128);
            entity.HasIndex(x => x.ShortCode).IsUnique();
            entity.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
            entity.HasIndex(x => new { x.IsActive, x.ExpiresAt });
        });

        modelBuilder.Entity<ClickEvent>(entity =>
        {
            entity.ToTable("click_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ShortCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Referrer).HasMaxLength(512);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.DeviceCategory).HasMaxLength(32);
            entity.Property(x => x.Country).HasMaxLength(8);
            entity.HasIndex(x => new { x.ShortCode, x.OccurredAt });
        });
    }
}
