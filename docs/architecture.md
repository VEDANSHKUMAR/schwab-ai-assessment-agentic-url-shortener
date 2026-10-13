# Architecture

## Product

Modular monolith with clear boundaries:

- **Api** — HTTP, auth middleware, rate limiting, health, static client
- **Application** — use cases (`UrlService`), FluentValidation, ports
- **Domain** — `ShortUrl`, `ClickEvent`, short-code generator, domain exceptions
- **Infrastructure** — EF Core/PostgreSQL, Redis cache-aside, analytics channel + worker
- **Orchestration** — agentic SDLC engine

## Redirect critical path

```
GET /{code} → validate → Redis GET
  HIT  → validate active/expiry → enqueue analytics → 302
  MISS → PostgreSQL → populate Redis → enqueue analytics → 302
```

Redis outage: log + metric, serve from PostgreSQL when possible.

## Orchestration control flow

```
Start run → mark Ready stages → execute batch
  → if RequiresApproval: WaitingApproval (human gate)
  → on approve: mark Succeeded, recompute Ready, continue
  → parallel group (data_model + api_contract) executes together then joins
  → SafeStop freezes further work
```

Context artifacts (`requirements.json`, `architecture.md`, …) accumulate as versioned decision lineage via `OrchestrationEvent` records.
