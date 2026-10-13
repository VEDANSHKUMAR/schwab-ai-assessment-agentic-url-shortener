# Grader / Reviewer Quick Start

This repository is the **Charles Schwab G55 AI Engineering Assessment** deliverable:

1. Working URL shortener (ASP.NET Core 8 + PostgreSQL + Redis)
2. Agentic SDLC orchestration engine (DAG, gates, retries, safe-stop, lineage, metrics)
3. Three scenarios: Greenfield, Brownfield, Ambiguous
4. React client, tests, ADRs, Docker Compose

## Fastest path (recommended)

```bash
docker compose up --build
```

Open:

- UI: http://localhost:5488/
- Swagger: http://localhost:5488/swagger
- Health: http://localhost:5488/health
- Ready: http://localhost:5488/ready

Default API key header: `X-Api-Key: dev-api-key-change-me`

### Demo the orchestrator (UI)

1. Open **Agentic Orchestration**
2. Select **Greenfield** → Start run
3. Click **Approve gate** until status is `Succeeded`
4. Repeat for **Brownfield** and **Ambiguous**

### Demo the product API

```bash
curl -s -X POST http://localhost:5488/api/v1/urls \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: dev-api-key-change-me" \
  -d '{"url":"https://example.com","customAlias":"demo1"}'

curl -sI http://localhost:5488/demo1
```

## Run tests

Requires PostgreSQL + Redis (Compose services or local).

```bash
dotnet test UrlShortener.sln
```

Suites:

- `UrlShortener.UnitTests` — domain, validation, orchestration gates
- `UrlShortener.IntegrationTests` — API + PostgreSQL + Redis (cache, conflicts, 410, auth)
- `UrlShortener.EndToEndTests` — create→redirect→analytics + all 3 orchestration scenarios

## Evaluation mapping

| Criterion | Where to look |
|-----------|----------------|
| Agentic orchestration | `src/UrlShortener.Orchestration`, UI tab, `/api/v1/orchestration/*` |
| Dependency graph + parallel sync | Greenfield design stages (`data_model` parallel with `api_contract`) |
| Human gates / safe-stop / reject | UI Approve / Reject / Safe stop |
| Dynamic re-plan | UI **Re-plan** or `POST .../replan` |
| Metrics (success/retry/rollback/MTTR/latency) | `GET /api/v1/orchestration/metrics` |
| Architecture quality | `docs/architecture.md`, modular `src/*` projects |
| Decomposition / scenarios | `docs/scenarios/*`, orchestration graphs |
| Validation / risk | FluentValidation, ProblemDetails, rate limits, soft-delete, ADRs |
| Controlled autonomy | Human approval gates before architecture/impl/release |
| Engineering summary | `docs/ENGINEERING_SUMMARY.md`, README |

## Known intentional prototype limits

- Analytics queue is in-process (Channel) — not crash-durable
- Orchestration run store is in-memory — persists for process lifetime
- Stage agents are deterministic by default (no LLM key required); `IStageAgent` is the extension point
