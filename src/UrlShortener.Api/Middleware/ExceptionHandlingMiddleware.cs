using System.Diagnostics;
using FluentValidation;
using UrlShortener.Domain.Exceptions;

namespace UrlShortener.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var (status, title, detail) = Map(ex);

        if (status >= 500)
        {
            _logger.LogError(ex, "Unhandled exception {TraceId}", traceId);
        }
        else
        {
            _logger.LogWarning(ex, "Request failed with {StatusCode} {TraceId}", status, traceId);
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = status;

        var problem = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.com/{status}",
            ["title"] = title,
            ["status"] = status,
            ["detail"] = detail,
            ["traceId"] = traceId
        };

        if (_env.IsDevelopment() && status >= 500)
        {
            problem["exception"] = ex.GetType().Name;
        }

        await context.Response.WriteAsJsonAsync(problem);
    }

    private static (int Status, string Title, string Detail) Map(Exception ex) => ex switch
    {
        ValidationException vex => (StatusCodes.Status400BadRequest, "Validation failed",
            string.Join("; ", vex.Errors.Select(e => e.ErrorMessage))),
        ValidationDomainException v => (StatusCodes.Status400BadRequest, "Validation failed", v.Message),
        NotFoundException n => (StatusCodes.Status404NotFound, "Resource not found", n.Message),
        ConflictException c => (StatusCodes.Status409Conflict, "Conflict", c.Message),
        GoneException g => (StatusCodes.Status410Gone, "Gone", g.Message),
        OperationCanceledException => (499, "Request cancelled", "The request was cancelled."),
        _ => (StatusCodes.Status500InternalServerError, "Internal Server Error",
            "An unexpected error occurred.")
    };
}
