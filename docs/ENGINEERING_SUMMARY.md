# Final Engineering Summary

## Objective completed

Built a reviewable engineering outcome for the Schwab AI Engineering Assessment:

- **Product:** LinkForge URL shortener (modular monolith)
- **Differentiator:** Agentic SDLC orchestration with dependency graph, parallel stages, human gates, bounded retries, safe-stop, policy guardrails, decision lineage, and reliability metrics
- **Scenarios:** Greenfield, Brownfield, Ambiguous — each executable via API/UI

## Plan / rationale

Chose a modular monolith over microservices to maximize correctness and demo clarity in an interview window while preserving extraction boundaries (redirect path, analytics worker, orchestration). Redis cache-aside protects the redirect hot path; analytics is asynchronous so it cannot block redirects. Short codes use cryptographically secure Base62 with DB uniqueness. Auth is API-key for management APIs to show real boundaries without OIDC scope explosion.

## Artifacts

- Runnable API + React client (`docker compose up --build`)
- OpenAPI/Swagger
- EF Core migrations (PostgreSQL)
- Unit + integration + end-to-end tests
- ADRs (`docs/adr/*`)
- Scenario write-ups (`docs/scenarios/*`)
- Security / architecture / orchestration docs

## Risks / trade-offs / validation

| Risk | Mitigation / validation |
|------|-------------------------|
| Alias race | Unique DB constraint + 409 mapping; integration test |
| Stale cache after disable | Explicit Redis invalidation; 410 test |
| Redis outage | Fall through to PostgreSQL; warn logs |
| Analytics loss on crash | Accepted prototype trade-off; Channel + worker; upgrade path documented |
| Ambiguous requirements | Ambiguity stage + human approval before architecture |
| Secret leakage in agent context | Policy guardrail blocks private-key/credential patterns + rollback |
| Upstream requirement change mid-run | Dynamic re-plan API marks downstream Stale and resumes under gates |
| Orchestration reliability | Metrics: success rate, retries, rollbacks, replans, MTTR, e2e/stage latency |

## Assumptions

- Graders can run Docker Compose or provide PostgreSQL 16 + Redis 7
- No LLM API key required for orchestration demo
- Browser redirect destinations are not server-fetched (SSRF model differs)

## Limitations

- In-memory orchestration store
- In-process analytics queue
- No geo-IP vendor
- CI workflow present; requires GitHub `workflow` scope if pushing via limited PAT

## Autonomy boundary

Agents execute multi-step SDLC stages and produce artifacts. Humans approve high-impact gates and own final quality. Safe-stop and reject paths are first-class.
