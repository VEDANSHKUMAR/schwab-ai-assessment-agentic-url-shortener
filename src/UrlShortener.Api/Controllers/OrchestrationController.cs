using Microsoft.AspNetCore.Mvc;
using UrlShortener.Orchestration.Models;
using UrlShortener.Orchestration.Services;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("api/v1/orchestration")]
public sealed class OrchestrationController : ControllerBase
{
    private readonly IOrchestrationService _orchestration;

    public OrchestrationController(IOrchestrationService orchestration) => _orchestration = orchestration;

    [HttpPost("runs")]
    public async Task<ActionResult<OrchestrationRunDto>> Start(
        [FromBody] StartRunRequest request,
        CancellationToken cancellationToken)
    {
        var run = await _orchestration.StartAsync(request, cancellationToken);
        var advanced = await _orchestration.TickAsync(run.Id, cancellationToken) ?? run;
        return CreatedAtAction(nameof(Get), new { id = advanced.Id }, advanced);
    }

    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<OrchestrationRunDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _orchestration.ListAsync(cancellationToken));

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<OrchestrationRunDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var run = await _orchestration.GetAsync(id, cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpPost("runs/{id:guid}/approve")]
    public async Task<ActionResult<OrchestrationRunDto>> Approve(
        Guid id,
        [FromBody] ApprovalRequest request,
        CancellationToken cancellationToken)
    {
        var run = await _orchestration.ApproveAsync(id, request, cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpPost("runs/{id:guid}/replan")]
    public async Task<ActionResult<OrchestrationRunDto>> Replan(
        Guid id,
        [FromBody] ReplanRequest request,
        CancellationToken cancellationToken)
    {
        var run = await _orchestration.ReplanAsync(id, request, cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpPost("runs/{id:guid}/stop")]
    public async Task<ActionResult<OrchestrationRunDto>> Stop(Guid id, CancellationToken cancellationToken)
    {
        var run = await _orchestration.StopAsync(id, cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpPost("runs/{id:guid}/tick")]
    public async Task<ActionResult<OrchestrationRunDto>> Tick(Guid id, CancellationToken cancellationToken)
    {
        var run = await _orchestration.TickAsync(id, cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpGet("metrics")]
    public ActionResult<OrchestrationMetrics> Metrics() => Ok(_orchestration.GetMetrics());
}
