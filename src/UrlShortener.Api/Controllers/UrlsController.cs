using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrlShortener.Api.Security;
using UrlShortener.Application.Urls;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("api/v1/urls")]
public sealed class UrlsController : ControllerBase
{
    private readonly IUrlService _urls;

    public UrlsController(IUrlService urls) => _urls = urls;

    [HttpPost]
    [EnableRateLimiting("create")]
    [ProducesResponseType(typeof(CreateShortUrlResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateShortUrlResponse>> Create(
        [FromBody] CreateShortUrlRequest request,
        CancellationToken cancellationToken)
    {
        var apiKey = HttpContext.Items[ApiKeyMiddleware.ItemKey]?.ToString();
        Request.Headers.TryGetValue("Idempotency-Key", out var idem);
        var result = await _urls.CreateAsync(request, apiKey, idem.ToString(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { shortCode = result.ShortCode }, result);
    }

    [HttpGet("{shortCode}")]
    [ProducesResponseType(typeof(ShortUrlDetailsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ShortUrlDetailsResponse>> Get(string shortCode, CancellationToken cancellationToken)
    {
        var result = await _urls.GetDetailsAsync(shortCode, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{shortCode}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Disable(string shortCode, CancellationToken cancellationToken)
    {
        await _urls.DisableAsync(shortCode, cancellationToken);
        return NoContent();
    }

    [HttpGet("{shortCode}/analytics")]
    [ProducesResponseType(typeof(AnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalyticsResponse>> Analytics(string shortCode, CancellationToken cancellationToken)
    {
        var result = await _urls.GetAnalyticsAsync(shortCode, cancellationToken);
        return Ok(result);
    }
}
