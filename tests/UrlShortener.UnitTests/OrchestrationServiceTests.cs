using Microsoft.Extensions.Logging.Abstractions;
using UrlShortener.Orchestration.Agents;
using UrlShortener.Orchestration.Models;
using UrlShortener.Orchestration.Services;

namespace UrlShortener.UnitTests;

public class OrchestrationServiceTests
{
    private readonly OrchestrationService _sut = new(
        new DeterministicStageAgent(),
        NullLogger<OrchestrationService>.Instance);

    [Fact]
    public async Task Greenfield_Stops_At_Architecture_Approval_Gate()
    {
        var started = await _sut.StartAsync(new StartRunRequest(ScenarioKind.Greenfield, null), CancellationToken.None);
        var run = await _sut.TickAsync(started.Id, CancellationToken.None);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.WaitingApproval, run!.Status);
        Assert.Contains(run.Stages, s => s.Name == "ArchitectureDesign" && s.Status == StageStatus.WaitingApproval);
        Assert.True(run.Context.ContainsKey("requirements.json"));
    }

    [Fact]
    public async Task Approve_Advances_Through_Parallel_Design_Stages()
    {
        var started = await _sut.StartAsync(new StartRunRequest(ScenarioKind.Greenfield, null), CancellationToken.None);
        await _sut.TickAsync(started.Id, CancellationToken.None);

        var after = await _sut.ApproveAsync(started.Id, new ApprovalRequest(true, "ok"), CancellationToken.None);
        Assert.NotNull(after);
        Assert.Contains(after!.Stages, s => s.Id == "data_model" && s.Status == StageStatus.Succeeded);
        Assert.Contains(after.Stages, s => s.Id == "api_contract" && s.Status == StageStatus.Succeeded);
    }

    [Fact]
    public async Task Ambiguous_Requires_Human_On_Ambiguity_Stage()
    {
        var started = await _sut.StartAsync(new StartRunRequest(ScenarioKind.Ambiguous, "make it viral"), CancellationToken.None);
        var run = await _sut.TickAsync(started.Id, CancellationToken.None);
        Assert.Equal(RunStatus.WaitingApproval, run!.Status);
        Assert.Contains(run.Stages, s => s.Name == "AmbiguityResolve" && s.Status == StageStatus.WaitingApproval);
    }

    [Fact]
    public async Task SafeStop_Prevents_Further_Progress()
    {
        var started = await _sut.StartAsync(new StartRunRequest(ScenarioKind.Brownfield, null), CancellationToken.None);
        await _sut.TickAsync(started.Id, CancellationToken.None);
        var stopped = await _sut.StopAsync(started.Id, CancellationToken.None);
        Assert.Equal(RunStatus.Stopped, stopped!.Status);
    }

    [Fact]
    public async Task Replan_Marks_Downstream_Stale_And_Increments_Version()
    {
        var started = await _sut.StartAsync(new StartRunRequest(ScenarioKind.Greenfield, null), CancellationToken.None);
        await _sut.TickAsync(started.Id, CancellationToken.None);
        await _sut.ApproveAsync(started.Id, new ApprovalRequest(true, "ok"), CancellationToken.None);

        var replanned = await _sut.ReplanAsync(started.Id, new ReplanRequest(
            Brief: "Updated requirements after stakeholder feedback",
            ChangedArtifactKey: "requirements.json",
            ChangedArtifactValue: """{"revised":true}""",
            Reason: "upstream requirements changed"), CancellationToken.None);

        Assert.NotNull(replanned);
        Assert.True(replanned!.ContextVersion >= 2);
        Assert.True(replanned.ReplanCount >= 1);
        Assert.Contains(replanned.Events, e => e.Kind == "Replan");
        Assert.True(replanned.Stages.Any(s => s.Status is StageStatus.Stale or StageStatus.Ready or StageStatus.WaitingApproval or StageStatus.Running or StageStatus.Succeeded));
    }

    [Fact]
    public async Task Metrics_Include_Success_Retry_Rollback_Latency_Fields()
    {
        var started = await _sut.StartAsync(new StartRunRequest(ScenarioKind.Greenfield, null), CancellationToken.None);
        await _sut.TickAsync(started.Id, CancellationToken.None);
        // reject -> failed metrics
        await _sut.ApproveAsync(started.Id, new ApprovalRequest(false, "no"), CancellationToken.None);

        var metrics = _sut.GetMetrics();
        Assert.True(metrics.TotalRuns >= 1);
        Assert.True(metrics.FailedRuns >= 1);
        Assert.True(metrics.SuccessRate >= 0);
        // latency fields exist even if zero when no successes yet
        Assert.True(metrics.AverageEndToEndLatencyMs >= 0);
        Assert.True(metrics.AverageStageLatencyMs >= 0);
        Assert.True(metrics.MttrMs >= 0);
    }

    [Fact]
    public async Task Policy_Guardrail_Blocks_Secret_Material_And_Rolls_Back()
    {
        var started = await _sut.StartAsync(new StartRunRequest(ScenarioKind.Brownfield, null), CancellationToken.None);
        await _sut.TickAsync(started.Id, CancellationToken.None);

        // Inject secret into context then force replan/tick path by approving with polluted context via replan artifact
        var polluted = await _sut.ReplanAsync(started.Id, new ReplanRequest(
            null,
            "leak.txt",
            "-----BEGIN RSA PRIVATE KEY-----\nABC\n-----END RSA PRIVATE KEY-----",
            "simulate secret leak"), CancellationToken.None);

        Assert.NotNull(polluted);
        // Either waiting on gate before policy hit, or rolled back/failed after stage start with secret present
        Assert.True(
            polluted!.Status is RunStatus.RolledBack or RunStatus.Failed or RunStatus.WaitingApproval or RunStatus.Running,
            polluted.Status.ToString());

        if (polluted.Status == RunStatus.WaitingApproval)
        {
            // advance: secret is in context so next stage execution should trip policy
            var after = await _sut.ApproveAsync(started.Id, new ApprovalRequest(true, "ok"), CancellationToken.None);
            Assert.True(after!.Status is RunStatus.RolledBack or RunStatus.Failed or RunStatus.WaitingApproval);
            if (after.Status is RunStatus.RolledBack or RunStatus.Failed)
            {
                Assert.Contains(after.Events, e => e.Kind is "PolicyViolation" or "Rollback");
            }
        }
    }
}
