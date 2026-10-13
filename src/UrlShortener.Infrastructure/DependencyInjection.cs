using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using UrlShortener.Application.Abstractions;
using UrlShortener.Infrastructure.Analytics;
using UrlShortener.Infrastructure.Caching;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5432;Database=urlshortener;Username=urlshortener;Password=urlshortener";

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUrlRepository, UrlRepository>();
        services.AddScoped<IClickEventRepository, ClickEventRepository>();

        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnection));
        services.AddSingleton<IUrlCache, RedisUrlCache>();

        services.AddSingleton<AnalyticsQueue>();
        services.AddSingleton<IAnalyticsQueue>(sp => sp.GetRequiredService<AnalyticsQueue>());
        services.AddSingleton<IAnalyticsPublisher>(sp => sp.GetRequiredService<AnalyticsQueue>());
        services.AddHostedService<AnalyticsWorker>();

        return services;
    }
}
