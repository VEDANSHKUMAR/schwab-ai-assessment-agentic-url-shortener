using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Orchestration.Agents;
using UrlShortener.Orchestration.Services;

namespace UrlShortener.Orchestration;

public static class DependencyInjection
{
    public static IServiceCollection AddOrchestration(this IServiceCollection services)
    {
        services.AddSingleton<IStageAgent, DeterministicStageAgent>();
        services.AddSingleton<IOrchestrationService, OrchestrationService>();
        return services;
    }
}
