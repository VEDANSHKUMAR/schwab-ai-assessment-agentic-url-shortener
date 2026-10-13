# API

Base URL (local): `http://127.0.0.1:5488`

Auth: `X-Api-Key: dev-api-key-change-me` for management endpoints. Redirects are public.

Optional: `Idempotency-Key` on `POST /api/v1/urls`.

## Create

`POST /api/v1/urls`

```json
{ "url": "https://example.com/path", "customAlias": "docs", "expiresAt": "2026-12-31T23:59:59Z" }
```

Responses: `201`, `400`, `401`, `409`, `429`.

## Redirect

`GET /{shortCode}` → `302` Location original URL. `404` / `410` as appropriate.

## Orchestration

`POST /api/v1/orchestration/runs` body: `{ "scenario": "Greenfield"|"Brownfield"|"Ambiguous", "brief": "..." }`

`POST /api/v1/orchestration/runs/{id}/approve` body: `{ "approve": true, "comment": "ok" }`
