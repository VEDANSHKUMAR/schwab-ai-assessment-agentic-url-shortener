using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Graph;

public static class ScenarioGraphFactory
{
    public static OrchestrationRun Create(ScenarioKind scenario, string? brief)
    {
        var run = new OrchestrationRun
        {
            Scenario = scenario,
            Brief = string.IsNullOrWhiteSpace(brief) ? DefaultBrief(scenario) : brief.Trim(),
            Status = RunStatus.Pending
        };

        run.Stages = scenario switch
        {
            ScenarioKind.Greenfield => GreenfieldStages(),
            ScenarioKind.Brownfield => BrownfieldStages(),
            ScenarioKind.Ambiguous => AmbiguousStages(),
            _ => GreenfieldStages()
        };

        run.Events.Add(new OrchestrationEvent
        {
            Kind = "RunCreated",
            Message = $"Created {scenario} orchestration run",
            Decision = "Human submitted scenario; agents will execute under gate controls"
        });

        MarkReadyStages(run);
        return run;
    }

    public static void MarkReadyStages(OrchestrationRun run)
    {
        var completed = run.Stages
            .Where(s => s.Status is StageStatus.Succeeded or StageStatus.Skipped)
            .Select(s => s.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var stage in run.Stages.Where(s => s.Status is StageStatus.Pending or StageStatus.Stale or StageStatus.Ready))
        {
            if (stage.DependsOn.All(d => completed.Contains(d)))
            {
                stage.Status = StageStatus.Ready;
            }
            else if (stage.Status is StageStatus.Ready or StageStatus.Stale)
            {
                stage.Status = StageStatus.Pending;
            }
        }
    }

    private static string DefaultBrief(ScenarioKind scenario) => scenario switch
    {
        ScenarioKind.Greenfield =>
            "Build a production-oriented URL shortener with create, redirect, custom aliases, expiration, soft-delete, async analytics, Redis cache-aside, and observability.",
        ScenarioKind.Brownfield =>
            "Enhance the existing shortener: harden custom-alias uniqueness under concurrency and ensure Redis cache invalidation on disable.",
        ScenarioKind.Ambiguous =>
            "Make short links better / viral / global.",
        _ => "URL shortener engineering scenario"
    };

    private static List<OrchestrationStage> GreenfieldStages() =>
    [
        Stage("requirements", "RequirementsNormalize", requiresApproval: false),
        Stage("ambiguity", "AmbiguityResolve", depends: ["requirements"]),
        Stage("architecture", "ArchitectureDesign", depends: ["ambiguity"], requiresApproval: true),
        Stage("data_model", "DataModelDesign", depends: ["architecture"], parallelGroup: "design"),
        Stage("api_contract", "ApiContractDesign", depends: ["architecture"], parallelGroup: "design"),
        Stage("impl_plan", "ImplementationPlan", depends: ["data_model", "api_contract"]),
        Stage("impl_execute", "ImplementationExecute", depends: ["impl_plan"], requiresApproval: true),
        Stage("test_plan", "TestPlan", depends: ["impl_execute"]),
        Stage("test_execute", "TestExecute", depends: ["test_plan"]),
        Stage("docs", "DocumentationGenerate", depends: ["test_execute"]),
        Stage("release", "ReleaseReadiness", depends: ["docs"], requiresApproval: true)
    ];

    private static List<OrchestrationStage> BrownfieldStages() =>
    [
        Stage("requirements", "RequirementsNormalize"),
        Stage("impact", "ImpactAnalysis", depends: ["requirements"]),
        Stage("architecture", "ArchitectureDesign", depends: ["impact"], requiresApproval: true),
        Stage("impl_plan", "ImplementationPlan", depends: ["architecture"]),
        Stage("impl_execute", "ImplementationExecute", depends: ["impl_plan"], requiresApproval: true),
        Stage("test_execute", "TestExecute", depends: ["impl_execute"]),
        Stage("docs", "DocumentationGenerate", depends: ["test_execute"]),
        Stage("release", "ReleaseReadiness", depends: ["docs"], requiresApproval: true)
    ];

    private static List<OrchestrationStage> AmbiguousStages() =>
    [
        Stage("requirements", "RequirementsNormalize"),
        Stage("ambiguity", "AmbiguityResolve", depends: ["requirements"], requiresApproval: true),
        Stage("architecture", "ArchitectureDesign", depends: ["ambiguity"], requiresApproval: true),
        Stage("api_contract", "ApiContractDesign", depends: ["architecture"]),
        Stage("impl_plan", "ImplementationPlan", depends: ["api_contract"]),
        Stage("impl_execute", "ImplementationExecute", depends: ["impl_plan"], requiresApproval: true),
        Stage("test_execute", "TestExecute", depends: ["impl_execute"]),
        Stage("docs", "DocumentationGenerate", depends: ["test_execute"]),
        Stage("release", "ReleaseReadiness", depends: ["docs"], requiresApproval: true)
    ];

    private static OrchestrationStage Stage(
        string id,
        string name,
        IEnumerable<string>? depends = null,
        bool requiresApproval = false,
        string? parallelGroup = null) =>
        new()
        {
            Id = id,
            Name = name,
            DependsOn = depends?.ToList() ?? [],
            RequiresApproval = requiresApproval,
            IsParallelGroup = parallelGroup is not null,
            ParallelGroup = parallelGroup,
            Status = StageStatus.Pending
        };
}
