# Agentic Orchestration

## Why not Kafka / Autogen cloud?

Interview prototype needs a **real governance model**, not a vendor demo. An in-process DAG with explicit gates, lineage, and metrics is auditable and runnable offline. Stage agents implement `IStageAgent` — swap in Azure OpenAI later without changing the graph runtime.

## Capabilities (mapped to assessment rubric)

| Capability | Implementation |
|------------|----------------|
| Dependency graph | `ScenarioGraphFactory` stages + `DependsOn` |
| Parallel + join | `ParallelGroup` batching then barrier via dependencies |
| Human gates | `RequiresApproval` → `WaitingApproval` |
| Retries | Bounded attempts on transient executor failures |
| Fallback | Deterministic agent always available |
| Rollback | Per-stage context snapshot restore + `RunStatus.RolledBack` |
| Safe-stop | `StopAsync` → `RunStatus.Stopped` |
| Policy | Blocks secrets / destructive ops in context |
| Observability | Events + lineage + `/api/v1/orchestration/metrics` |
| Metrics | success rate, retries, rollbacks, replans, **MTTR**, **avg e2e latency**, **avg stage latency** |
| Dynamic re-plan | `POST /api/v1/orchestration/runs/{id}/replan` marks downstream `Stale`, bumps context version, resumes under gates |

## Re-plan control flow

```
Upstream artifact/brief changes
  → snapshot context
  → bump ContextVersion
  → mark pivot + downstream stages Stale
  → MarkReadyStages
  → Tick (may hit approval gates again)
```

## Autonomy boundary

Agents produce artifacts and advance stages. Humans approve high-impact stages (architecture, implementation execute, release, ambiguity resolution) and may re-plan or safe-stop at any time.
