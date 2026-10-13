namespace UrlShortener.Orchestration.Models;

public enum StageStatus
{
    Pending,
    Ready,
    Running,
    WaitingApproval,
    Succeeded,
    Failed,
    RolledBack,
    Skipped,
    Stale
}

public enum RunStatus
{
    Pending,
    Running,
    WaitingApproval,
    Succeeded,
    Failed,
    Stopped,
    RolledBack
}

public enum ScenarioKind
{
    Greenfield,
    Brownfield,
    Ambiguous
}

public sealed class OrchestrationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ScenarioKind Scenario { get; set; }
    public RunStatus Status { get; set; } = RunStatus.Pending;
    public string Brief { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
    public int RollbackCount { get; set; }
    public int ReplanCount { get; set; }
    public int ContextVersion { get; set; } = 1;
    public Dictionary<string, string> Context { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<OrchestrationStage> Stages { get; set; } = [];
    public List<OrchestrationEvent> Events { get; set; } = [];
}

public sealed class OrchestrationStage
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public StageStatus Status { get; set; } = StageStatus.Pending;
    public List<string> DependsOn { get; set; } = [];
    public bool RequiresApproval { get; set; }
    public bool IsParallelGroup { get; set; }
    public string? ParallelGroup { get; set; }
    public int Attempt { get; set; }
    public string? OutputSummary { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public double? DurationMs { get; set; }
}

public sealed class OrchestrationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public string Kind { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StageId { get; set; }
    public string? Decision { get; set; }
}

public sealed record StartRunRequest(ScenarioKind Scenario, string? Brief);

public sealed record ApprovalRequest(bool Approve, string? Comment);

public sealed record ReplanRequest(
    string? Brief,
    string? ChangedArtifactKey,
    string? ChangedArtifactValue,
    string? Reason);

public sealed record OrchestrationRunDto(
    Guid Id,
    ScenarioKind Scenario,
    RunStatus Status,
    string Brief,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason,
    int RetryCount,
    int RollbackCount,
    int ReplanCount,
    int ContextVersion,
    double? EndToEndLatencyMs,
    IReadOnlyDictionary<string, string> Context,
    IReadOnlyList<OrchestrationStage> Stages,
    IReadOnlyList<OrchestrationEvent> Events);
