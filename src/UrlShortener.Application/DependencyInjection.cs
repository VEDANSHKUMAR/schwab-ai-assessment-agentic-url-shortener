using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Application.Urls;
using UrlShortener.Domain.Services;

namespace UrlShortener.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IShortCodeGenerator, ShortCodeGenerator>();
        services.AddScoped<IUrlService, UrlService>();
        services.AddValidatorsFromAssemblyContaining<CreateShortUrlValidator>();
        return services;
    }
}
