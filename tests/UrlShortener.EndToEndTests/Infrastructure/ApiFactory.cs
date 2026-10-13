using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace UrlShortener.EndToEndTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var postgres = Environment.GetEnvironmentVariable("TEST_POSTGRES")
                ?? "Host=localhost;Port=5432;Database=urlshortener;Username=urlshortener;Password=urlshortener";
            var redis = Environment.GetEnvironmentVariable("TEST_REDIS") ?? "localhost:6379";

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = postgres,
                ["ConnectionStrings:Redis"] = redis,
                ["UrlShortener:BaseUrl"] = "http://localhost",
                ["UrlShortener:ApiKeys:0"] = "test-api-key",
                ["UrlShortener:CreateRateLimitPerMinute"] = "10000",
                ["UrlShortener:RedirectRateLimitPerMinute"] = "10000",
                ["EnableSwagger"] = "false"
            });
        });
    }
}
