# LinkForge — Agentic URL Shortener

Production-oriented **URL shortener** (ASP.NET Core 8, PostgreSQL, Redis) plus an **agentic SDLC orchestration engine** that demonstrates governed, non-linear engineering workflows for the AI Engineering Assessment.

**Project location in this cloud workspace:** `/workspace`  
On your Windows machine, clone/sync the repo (for example under `C:\Users\DELL\...`). You do not need to recreate files manually on `C:\Users\DELL` — use Git.

## What this demonstrates

1. **URL shortener product** — create, redirect, custom aliases, expiration, soft-disable, async analytics, Redis cache-aside, API keys, rate limits, health/ready, ProblemDetails, Serilog.
2. **Agentic orchestration** — explicit dependency graph, parallel stages + sync, human approval gates, bounded retries, safe-stop, policy guardrails, decision lineage, reliability metrics.
3. **Three scenarios** — Greenfield, Brownfield, Ambiguous (runnable from the web client or API).
4. **Web client** — React (Vite) UI for shortening links and driving orchestration approvals.

## For graders / reviewers

See **[docs/GRADING_GUIDE.md](docs/GRADING_GUIDE.md)** and **[docs/ENGINEERING_SUMMARY.md](docs/ENGINEERING_SUMMARY.md)**.

```bash
docker compose up --build
# UI http://localhost:5488  |  tests: dotnet test UrlShortener.sln
```

## Quick start (local without Docker Compose)

Prerequisites (install from verified vendors):

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js LTS](https://nodejs.org/) (for client build)
- PostgreSQL 16 + Redis 7 (or use Docker Compose below)
- Optional: [Docker Desktop](https://www.docker.com/products/docker-desktop/) from docker.com

```bash
# Database (example)
# Create DB/user: urlshortener / urlshortener

dotnet restore UrlShortener.sln
dotnet test UrlShortener.sln
cd client && npm install && npm run build && cd ..
dotnet run --project src/UrlShortener.Api --urls http://127.0.0.1:5488
./scripts/grader-smoke.sh http://127.0.0.1:5488
```

Open:

- Client: http://127.0.0.1:5488/
- Swagger: http://127.0.0.1:5488/swagger
- Health: http://127.0.0.1:5488/health
- Ready: http://127.0.0.1:5488/ready

Default API key: `dev-api-key-change-me` (header `X-Api-Key`).

## Docker Compose

Images: `postgres:16-alpine`, `redis:7-alpine` (Docker Hub official), `mcr.microsoft.com/dotnet/*` (Microsoft).

```bash
docker compose up --build
```

API + UI: http://localhost:5488

## Architecture (short)

```
Client (React)
    |
    v
ASP.NET Core API  ---- Orchestration Engine (DAG + gates)
    |                         |
    +-- URL Management        +-- Stage agents (deterministic; LLM optional later)
    +-- Redirect (Redis → PG)
    +-- Analytics Channel → BackgroundWorker → PostgreSQL
```

Redirect path never waits on analytics persistence. Redis failures fall back to PostgreSQL.

## API (core)

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/v1/urls` | Create (API key, rate limited) |
| GET | `/api/v1/urls/{code}` | Details (API key) |
| DELETE | `/api/v1/urls/{code}` | Soft disable + cache bust |
| GET | `/api/v1/urls/{code}/analytics` | Aggregates |
| GET | `/{code}` | 302 redirect |
| POST | `/api/v1/orchestration/runs` | Start scenario |
| POST | `/api/v1/orchestration/runs/{id}/approve` | Human gate |
| POST | `/api/v1/orchestration/runs/{id}/stop` | Safe-stop |
| GET | `/api/v1/orchestration/metrics` | Success/retry/rollback |

## Scenarios

| Scenario | What it shows |
|----------|----------------|
| Greenfield | Full SDLC graph from brief → design (parallel) → implement → test → docs → release |
| Brownfield | Impact analysis + focused change path with approvals |
| Ambiguous | Ambiguity stage pauses for human clarification before architecture |

## Configuration

`appsettings.json` / env vars:

- `ConnectionStrings__Postgres`
- `ConnectionStrings__Redis`
- `UrlShortener__BaseUrl`
- `UrlShortener__ApiKeys__0`
- `UrlShortener__CacheTtlSeconds`
- `UrlShortener__BlockedDomains__0`

## Documentation

- [docs/architecture.md](docs/architecture.md)
- [docs/api.md](docs/api.md)
- [docs/database.md](docs/database.md)
- [docs/security.md](docs/security.md)
- [docs/orchestration.md](docs/orchestration.md)
- [docs/adr/](docs/adr/)
- [docs/scenarios/](docs/scenarios/)

## Testing

```bash
dotnet test tests/UrlShortener.UnitTests
```

Integration/E2E projects are scaffolded for Testcontainers when Docker is available on the runner.

## Engineering summary (interview)

> Modular .NET URL shortener with Redis cache-aside redirects, async analytics, PostgreSQL uniqueness constraints, centralized ProblemDetails, and a governed agentic SDLC orchestrator with dependency graphs, human gates, retries/safe-stop, lineage, and metrics. Agents execute under autonomy boundaries; humans own approvals and final quality.

## Known limitations

- Analytics queue is in-process (not durable across process crash).
- Orchestration run state is in-memory (restart clears runs) — sufficient for prototype; swap to PostgreSQL tables for production.
- No geo-IP enrichment (extension point only).
- Stage agents are deterministic templates (no LLM required); plug in Azure OpenAI behind `IStageAgent` when desired.
- Docker CLI may be unavailable in some cloud agent VMs; Compose files are provided for your Windows/Docker Desktop host.

## Deploy later (Azure)

Map the same env vars to Azure Container Apps / App Service settings, use Azure Database for PostgreSQL + Azure Cache for Redis, store API keys in Key Vault references.
