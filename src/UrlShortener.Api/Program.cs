using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UrlShortener.Api.Middleware;
using UrlShortener.Api.Security;
using UrlShortener.Application;
using UrlShortener.Application.Options;
using UrlShortener.Infrastructure;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.Orchestration;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "UrlShortener")
        .WriteTo.Console());

    builder.Services.Configure<UrlShortenerOptions>(
        builder.Configuration.GetSection(UrlShortenerOptions.SectionName));

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddOrchestration();

    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
            o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "URL Shortener + Agentic Orchestration", Version = "v1" });
        c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "API key via X-Api-Key header",
            Name = "X-Api-Key",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
        });
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "ApiKey"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("client", policy =>
            policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("create", httpContext =>
        {
            var opts = httpContext.RequestServices
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<UrlShortenerOptions>>().Value;
            return RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, opts.CreateRateLimitPerMinute),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
        });
        options.AddPolicy("redirect", httpContext =>
        {
            var opts = httpContext.RequestServices
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<UrlShortenerOptions>>().Value;
            return RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, opts.RedirectRateLimitPerMinute),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
        });
    });

    var connectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? "Host=localhost;Port=5432;Database=urlshortener;Username=urlshortener;Password=urlshortener";
    var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgres")
        .AddRedis(redisConnection, name: "redis");

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment() || app.Configuration.GetValue("EnableSwagger", true))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors("client");
    app.UseRateLimiter();
    app.UseMiddleware<ApiKeyMiddleware>();

    var configuredClient = app.Configuration["Client:DistPath"];
    var clientPath = !string.IsNullOrWhiteSpace(configuredClient)
        ? Path.GetFullPath(configuredClient)
        : Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "client", "dist"));
    var clientAvailable = Directory.Exists(clientPath);
    if (clientAvailable)
    {
        var files = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(clientPath);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
    }

    app.MapControllers();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = async (ctx, _) =>
        {
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"status":"Healthy"}""");
        }
    });

    app.MapHealthChecks("/ready", new HealthCheckOptions
    {
        ResponseWriter = async (ctx, report) =>
        {
            ctx.Response.ContentType = "application/json";
            var payload = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString()
                })
            };
            await ctx.Response.WriteAsJsonAsync(payload);
        }
    });

    if (clientAvailable)
    {
        app.MapGet("/", async ctx =>
        {
            var index = Path.Combine(clientPath, "index.html");
            ctx.Response.ContentType = "text/html";
            await ctx.Response.SendFileAsync(index);
        });
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
