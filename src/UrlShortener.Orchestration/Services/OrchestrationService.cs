using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UrlShortener.Orchestration.Agents;
using UrlShortener.Orchestration.Graph;
using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Services;

public interface IOrchestrationService
{
    Task<OrchestrationRunDto> StartAsync(StartRunRequest request, CancellationToken cancellationToken);
    Task<OrchestrationRunDto?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrchestrationRunDto>> ListAsync(CancellationToken cancellationToken);
    Task<OrchestrationRunDto?> ApproveAsync(Guid id, ApprovalRequest request, CancellationToken cancellationToken);
    Task<OrchestrationRunDto?> StopAsync(Guid id, CancellationToken cancellationToken);
    Task<OrchestrationRunDto?> TickAsync(Guid id, CancellationToken cancellationToken);
    Task<OrchestrationRunDto?> ReplanAsync(Guid id, ReplanRequest request, CancellationToken cancellationToken);
    OrchestrationMetrics GetMetrics();
}

public sealed class OrchestrationMetrics
{
    public long TotalRuns { get; set; }
    public long SucceededRuns { get; set; }
    public long FailedRuns { get; set; }
    public long RetryCount { get; set; }
    public long RollbackCount { get; set; }
    public long ReplanCount { get; set; }
    public double SuccessRate { get; set; }
    /// <summary>Mean time to recover from failed/rolled-back runs that later succeed via replan+completion (ms).</summary>
    public double MttrMs { get; set; }
    /// <summary>Average end-to-end latency for completed (succeeded) runs (ms).</summary>
    public double AverageEndToEndLatencyMs { get; set; }
    /// <summary>Average stage execution latency across completed stages (ms).</summary>
    public double AverageStageLatencyMs { get; set; }
}

public sealed class OrchestrationService : IOrchestrationService
{
    private readonly ConcurrentDictionary<Guid, OrchestrationRun> _runs = new();
    private readonly IStageAgent _agent;
    private readonly ILogger<OrchestrationService> _logger;
    private readonly object _metricsLock = new();

    private long _totalRuns;
    private long _succeededRuns;
    private long _failedRuns;
    private long _retryCount;
    private long _rollbackCount;
    private long _replanCount;
    private readonly List<double> _e2eLatenciesMs = [];
    private readonly List<double> _stageLatenciesMs = [];
    private readonly List<double> _recoveryLatenciesMs = [];
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _failureOpenedAt = new();

    public OrchestrationService(IStageAgent agent, ILogger<OrchestrationService> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    public Task<OrchestrationRunDto> StartAsync(StartRunRequest request, CancellationToken cancellationToken)
    {
        var run = ScenarioGraphFactory.Create(request.Scenario, request.Brief);
        run.Context["__lineage_root"] = run.Id.ToString("N");
        run.Context["brief.v1"] = run.Brief;
        _runs[run.Id] = run;
        lock (_metricsLock)
        {
            _totalRuns++;
        }

        _logger.LogInformation("Started orchestration run {RunId} scenario {Scenario}", run.Id, run.Scenario);
        return Task.FromResult(ToDto(run));
    }

    public Task<OrchestrationRunDto?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_runs.TryGetValue(id, out var run) ? ToDto(run) : null);

    public Task<IReadOnlyList<OrchestrationRunDto>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OrchestrationRunDto>>(
            _runs.Values.OrderByDescending(r => r.CreatedAt).Select(ToDto).ToList());

    public async Task<OrchestrationRunDto?> ApproveAsync(Guid id, ApprovalRequest request, CancellationToken cancellationToken)
    {
        if (!_runs.TryGetValue(id, out var run))
        {
            return null;
        }

        if (run.Status is RunStatus.Stopped or RunStatus.Succeeded)
        {
            return ToDto(run);
        }

        var waiting = run.Stages.FirstOrDefault(s => s.Status == StageStatus.WaitingApproval);
        if (waiting is null)
        {
            return ToDto(run);
        }

        if (!request.Approve)
        {
            waiting.Status = StageStatus.Failed;
            waiting.Error = request.Comment ?? "Rejected by human reviewer";
            waiting.CompletedAt = DateTimeOffset.UtcNow;
            MarkFailed(run, waiting.Error);
            AddEvent(run, "ApprovalRejected", waiting.Error, waiting.Id, request.Comment);
            return ToDto(run);
        }

        waiting.Status = StageStatus.Succeeded;
        waiting.CompletedAt = DateTimeOffset.UtcNow;
        if (waiting.StartedAt is not null)
        {
            waiting.DurationMs = (waiting.CompletedAt.Value - waiting.StartedAt.Value).TotalMilliseconds;
            RecordStageLatency(waiting.DurationMs.Value);
        }

        waiting.OutputSummary = (waiting.OutputSummary ?? "") + " | Approved by human";
        AddEvent(run, "ApprovalGranted", $"Approved stage {waiting.Name}", waiting.Id, request.Comment ?? "approved");
        ScenarioGraphFactory.MarkReadyStages(run);
        run.Status = RunStatus.Running;
        run.UpdatedAt = DateTimeOffset.UtcNow;

        return await TickAsync(id, cancellationToken);
    }

    public Task<OrchestrationRunDto?> StopAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!_runs.TryGetValue(id, out var run))
        {
            return Task.FromResult<OrchestrationRunDto?>(null);
        }

        run.Status = RunStatus.Stopped;
        run.CompletedAt = DateTimeOffset.UtcNow;
        AddEvent(run, "SafeStop", "Human issued safe-stop; no further high-impact stages will start", null, "safe-stop");
        return Task.FromResult<OrchestrationRunDto?>(ToDto(run));
    }

    public async Task<OrchestrationRunDto?> ReplanAsync(Guid id, ReplanRequest request, CancellationToken cancellationToken)
    {
        if (!_runs.TryGetValue(id, out var run))
        {
            return null;
        }

        if (run.Status is RunStatus.Stopped)
        {
            return ToDto(run);
        }

        // Snapshot before replan for rollback lineage
        run.Context[$"__snapshot_before_replan_v{run.ContextVersion}"] =
            JsonSerializer.Serialize(run.Context.Where(kv => !kv.Key.StartsWith("__snapshot_", StringComparison.Ordinal))
                .ToDictionary(kv => kv.Key, kv => kv.Value));

        if (!string.IsNullOrWhiteSpace(request.Brief))
        {
            run.Brief = request.Brief.Trim();
            run.Context[$"brief.v{run.ContextVersion + 1}"] = run.Brief;
        }

        if (!string.IsNullOrWhiteSpace(request.ChangedArtifactKey)
            && request.ChangedArtifactValue is not null)
        {
            run.Context[request.ChangedArtifactKey] = request.ChangedArtifactValue;
        }

        run.ContextVersion++;
        run.ReplanCount++;
        lock (_metricsLock)
        {
            _replanCount++;
        }

        // Dynamic re-plan: invalidate downstream of the first completed stage that produced requirements/architecture,
        // or invalidate everything after requirements if brief changed.
        var invalidateFrom = ResolveInvalidateFrom(run, request.ChangedArtifactKey);
        MarkDownstreamStale(run, invalidateFrom);

        // Clear terminal failure so governed execution can resume
        if (run.Status is RunStatus.Failed or RunStatus.RolledBack or RunStatus.Succeeded)
        {
            run.Status = RunStatus.Running;
            run.FailureReason = null;
            run.CompletedAt = null;
        }

        ScenarioGraphFactory.MarkReadyStages(run);
        AddEvent(
            run,
            "Replan",
            $"Re-planned from '{invalidateFrom}' due to upstream change (v{run.ContextVersion}). {request.Reason ?? "upstream artifact updated"}",
            invalidateFrom,
            "replan");

        _logger.LogInformation("Replanned run {RunId} from stage {StageId} to context v{Version}", run.Id, invalidateFrom, run.ContextVersion);

        // Continue under governance (may hit approval gates again)
        return await TickAsync(id, cancellationToken);
    }

    public async Task<OrchestrationRunDto?> TickAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!_runs.TryGetValue(id, out var run))
        {
            return null;
        }

        if (run.Status is RunStatus.Succeeded or RunStatus.Failed or RunStatus.Stopped or RunStatus.RolledBack)
        {
            return ToDto(run);
        }

        if (run.Stages.Any(s => s.Status == StageStatus.WaitingApproval))
        {
            run.Status = RunStatus.WaitingApproval;
            return ToDto(run);
        }

        run.Status = RunStatus.Running;

        var ready = run.Stages.Where(s => s.Status == StageStatus.Ready).ToList();
        if (ready.Count == 0)
        {
            if (run.Stages.All(s => s.Status is StageStatus.Succeeded or StageStatus.Skipped))
            {
                MarkSucceeded(run);
            }

            return ToDto(run);
        }

        var parallelGroup = ready.FirstOrDefault(s => s.IsParallelGroup)?.ParallelGroup;
        var batch = parallelGroup is null
            ? ready.Take(1).ToList()
            : ready.Where(s => s.ParallelGroup == parallelGroup).ToList();

        foreach (var stage in batch)
        {
            if (run.Status == RunStatus.Stopped)
            {
                break;
            }

            await ExecuteStageAsync(run, stage, cancellationToken);
            if (stage.Status == StageStatus.WaitingApproval)
            {
                run.Status = RunStatus.WaitingApproval;
                return ToDto(run);
            }

            if (stage.Status is StageStatus.Failed or StageStatus.RolledBack
                || run.Status is RunStatus.Failed or RunStatus.RolledBack)
            {
                return ToDto(run);
            }
        }

        ScenarioGraphFactory.MarkReadyStages(run);
        run.UpdatedAt = DateTimeOffset.UtcNow;

        if (run.Status == RunStatus.Running
            && run.Stages.Any(s => s.Status == StageStatus.Ready)
            && !run.Stages.Any(s => s.Status == StageStatus.WaitingApproval))
        {
            return await TickAsync(id, cancellationToken);
        }

        if (run.Stages.All(s => s.Status is StageStatus.Succeeded or StageStatus.Skipped))
        {
            MarkSucceeded(run);
        }

        return ToDto(run);
    }

    public OrchestrationMetrics GetMetrics()
    {
        lock (_metricsLock)
        {
            return new OrchestrationMetrics
            {
                TotalRuns = _totalRuns,
                SucceededRuns = _succeededRuns,
                FailedRuns = _failedRuns,
                RetryCount = _retryCount,
                RollbackCount = _rollbackCount,
                ReplanCount = _replanCount,
                SuccessRate = _totalRuns == 0 ? 0 : (double)_succeededRuns / _totalRuns,
                MttrMs = _recoveryLatenciesMs.Count == 0 ? 0 : _recoveryLatenciesMs.Average(),
                AverageEndToEndLatencyMs = _e2eLatenciesMs.Count == 0 ? 0 : _e2eLatenciesMs.Average(),
                AverageStageLatencyMs = _stageLatenciesMs.Count == 0 ? 0 : _stageLatenciesMs.Average()
            };
        }
    }

    private async Task ExecuteStageAsync(OrchestrationRun run, OrchestrationStage stage, CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;
        stage.Status = StageStatus.Running;
        stage.StartedAt = DateTimeOffset.UtcNow;
        stage.CompletedAt = null;
        stage.DurationMs = null;
        stage.Error = null;
        stage.Attempt++;

        // Always snapshot context before mutating stage outputs (enables rollback)
        var snapshotKey = "__snapshot_before_" + stage.Id;
        run.Context[snapshotKey] = JsonSerializer.Serialize(
            run.Context.Where(kv => !kv.Key.StartsWith("__snapshot_", StringComparison.Ordinal))
                .ToDictionary(kv => kv.Key, kv => kv.Value));

        AddEvent(run, "StageStarted", $"Starting {stage.Name} (attempt {stage.Attempt})", stage.Id, null);

        if (ViolatesPolicy(run, out var policyError))
        {
            stage.Status = StageStatus.Failed;
            stage.Error = policyError;
            stage.CompletedAt = DateTimeOffset.UtcNow;
            AddEvent(run, "PolicyViolation", policyError!, stage.Id, "blocked");
            PerformRollback(run, stage);
            return;
        }

        StageExecutionResult? result = null;
        Exception? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                result = await _agent.ExecuteAsync(run, stage, cancellationToken);
                if (result.Success)
                {
                    break;
                }

                run.RetryCount++;
                lock (_metricsLock)
                {
                    _retryCount++;
                }

                AddEvent(run, "StageRetry", $"Unsuccessful agent result on {stage.Name}", stage.Id, "retry");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                run.RetryCount++;
                lock (_metricsLock)
                {
                    _retryCount++;
                }

                AddEvent(run, "StageRetry", $"Transient failure on {stage.Name}: {ex.Message}", stage.Id, "retry");
            }
        }

        if (result is null || !result.Success)
        {
            stage.Status = StageStatus.Failed;
            stage.Error = result?.Error ?? lastError?.Message ?? "Stage failed";
            stage.CompletedAt = DateTimeOffset.UtcNow;
            stage.DurationMs = (stage.CompletedAt.Value - stage.StartedAt.Value).TotalMilliseconds;
            RecordStageLatency(stage.DurationMs.Value);
            AddEvent(run, "StageFailed", stage.Error, stage.Id, "failed");
            PerformRollback(run, stage);
            return;
        }

        if (result.ContextUpdates is not null)
        {
            foreach (var (k, v) in result.ContextUpdates)
            {
                run.Context[k] = v;
            }
        }

        stage.OutputSummary = result.Summary;

        if (stage.RequiresApproval)
        {
            stage.Status = StageStatus.WaitingApproval;
            AddEvent(run, "WaitingApproval", $"Human approval required for {stage.Name}", stage.Id, "gate");
            return;
        }

        stage.Status = StageStatus.Succeeded;
        stage.CompletedAt = DateTimeOffset.UtcNow;
        stage.DurationMs = (stage.CompletedAt.Value - stage.StartedAt.Value).TotalMilliseconds;
        RecordStageLatency(stage.DurationMs.Value);
        AddEvent(run, "StageSucceeded", result.Summary, stage.Id, "success");
    }

    private void PerformRollback(OrchestrationRun run, OrchestrationStage stage)
    {
        var snapshotKey = "__snapshot_before_" + stage.Id;
        if (run.Context.TryGetValue(snapshotKey, out var snapshotJson))
        {
            try
            {
                var restored = JsonSerializer.Deserialize<Dictionary<string, string>>(snapshotJson);
                if (restored is not null)
                {
                    var snapshots = run.Context
                        .Where(kv => kv.Key.StartsWith("__snapshot_", StringComparison.Ordinal))
                        .ToList();
                    run.Context.Clear();
                    foreach (var (k, v) in restored)
                    {
                        run.Context[k] = v;
                    }

                    foreach (var (k, v) in snapshots)
                    {
                        run.Context[k] = v;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize rollback snapshot for {StageId}", stage.Id);
            }
        }

        stage.Status = StageStatus.RolledBack;
        run.RollbackCount++;
        lock (_metricsLock)
        {
            _rollbackCount++;
        }

        MarkFailed(run, stage.Error ?? $"Rolled back after failure in {stage.Name}");
        run.Status = RunStatus.RolledBack;
        AddEvent(run, "Rollback", $"Restored context snapshot after failure in {stage.Name}", stage.Id, "rollback");
    }

    private static bool ViolatesPolicy(OrchestrationRun run, out string? error)
    {
        foreach (var value in run.Context.Values)
        {
            if (value.Contains("BEGIN RSA PRIVATE KEY", StringComparison.Ordinal)
                || value.Contains("BEGIN OPENSSH PRIVATE KEY", StringComparison.Ordinal)
                || value.Contains("AKIA", StringComparison.Ordinal) // crude AWS key pattern
                || value.Contains("password=", StringComparison.OrdinalIgnoreCase)
                   && value.Contains("secret", StringComparison.OrdinalIgnoreCase))
            {
                error = "Policy guardrail blocked secret/credential material in orchestration context (change-control/security).";
                return true;
            }
        }

        if (run.Context.TryGetValue("implementation-status.json", out var impl)
            && impl.Contains("\"drop_production_database\":true", StringComparison.OrdinalIgnoreCase))
        {
            error = "Policy guardrail blocked destructive production database drop without change-control approval.";
            return true;
        }

        error = null;
        return false;
    }

    private static string ResolveInvalidateFrom(OrchestrationRun run, string? changedArtifactKey)
    {
        if (string.IsNullOrWhiteSpace(changedArtifactKey))
        {
            return "requirements";
        }

        var key = changedArtifactKey.ToLowerInvariant();
        if (key.Contains("requirement") || key.Contains("brief") || key.Contains("ambig"))
        {
            return "requirements";
        }

        if (key.Contains("architecture") || key.Contains("impact"))
        {
            return run.Stages.Any(s => s.Id == "architecture") ? "architecture" : "requirements";
        }

        if (key.Contains("api") || key.Contains("data") || key.Contains("database"))
        {
            return run.Stages.Any(s => s.Id is "data_model" or "api_contract") ? "architecture" : "requirements";
        }

        return "requirements";
    }

    private static void MarkDownstreamStale(OrchestrationRun run, string fromStageId)
    {
        var order = run.Stages.Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i, StringComparer.OrdinalIgnoreCase);
        if (!order.TryGetValue(fromStageId, out var fromIndex))
        {
            fromIndex = 0;
            fromStageId = run.Stages[0].Id;
        }

        foreach (var stage in run.Stages)
        {
            if (!order.TryGetValue(stage.Id, out var idx))
            {
                continue;
            }

            if (idx > fromIndex
                && stage.Status is StageStatus.Succeeded or StageStatus.Failed or StageStatus.RolledBack
                    or StageStatus.Skipped or StageStatus.WaitingApproval or StageStatus.Ready or StageStatus.Running)
            {
                stage.Status = StageStatus.Stale;
                stage.Error = null;
                stage.OutputSummary = (stage.OutputSummary ?? "") + " | invalidated by replan";
                stage.CompletedAt = null;
                stage.StartedAt = null;
                stage.DurationMs = null;
            }
        }

        // The pivot stage itself is re-executed
        var pivot = run.Stages.FirstOrDefault(s => s.Id.Equals(fromStageId, StringComparison.OrdinalIgnoreCase));
        if (pivot is not null
            && pivot.Status is StageStatus.Succeeded or StageStatus.Failed or StageStatus.RolledBack
                or StageStatus.WaitingApproval or StageStatus.Stale)
        {
            pivot.Status = StageStatus.Stale;
            pivot.CompletedAt = null;
            pivot.StartedAt = null;
            pivot.DurationMs = null;
            pivot.Error = null;
        }
    }

    private void MarkFailed(OrchestrationRun run, string reason)
    {
        run.Status = RunStatus.Failed;
        run.FailureReason = reason;
        run.CompletedAt = DateTimeOffset.UtcNow;
        _failureOpenedAt[run.Id] = run.CompletedAt.Value;
        lock (_metricsLock)
        {
            _failedRuns++;
        }
    }

    private void MarkSucceeded(OrchestrationRun run)
    {
        run.Status = RunStatus.Succeeded;
        run.CompletedAt = DateTimeOffset.UtcNow;
        var e2e = (run.CompletedAt.Value - run.CreatedAt).TotalMilliseconds;
        lock (_metricsLock)
        {
            _succeededRuns++;
            _e2eLatenciesMs.Add(e2e);
            if (_failureOpenedAt.TryRemove(run.Id, out var failedAt))
            {
                _recoveryLatenciesMs.Add((run.CompletedAt.Value - failedAt).TotalMilliseconds);
            }
        }

        AddEvent(run, "RunSucceeded", $"All stages completed in {e2e:F0}ms", null, "success");
    }

    private void RecordStageLatency(double ms)
    {
        lock (_metricsLock)
        {
            _stageLatenciesMs.Add(ms);
        }
    }

    private static void AddEvent(OrchestrationRun run, string kind, string message, string? stageId, string? decision)
    {
        run.Events.Add(new OrchestrationEvent
        {
            Kind = kind,
            Message = message,
            StageId = stageId,
            Decision = decision
        });
        run.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static OrchestrationRunDto ToDto(OrchestrationRun run)
    {
        double? e2e = run.CompletedAt is null
            ? null
            : (run.CompletedAt.Value - run.CreatedAt).TotalMilliseconds;

        return new(
            run.Id,
            run.Scenario,
            run.Status,
            run.Brief,
            run.CreatedAt,
            run.UpdatedAt,
            run.CompletedAt,
            run.FailureReason,
            run.RetryCount,
            run.RollbackCount,
            run.ReplanCount,
            run.ContextVersion,
            e2e,
            run.Context,
            run.Stages,
            run.Events);
    }
}
