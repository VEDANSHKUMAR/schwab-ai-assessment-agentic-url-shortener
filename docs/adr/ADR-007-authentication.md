# ADR-007: Authentication

## Decision
API key (`X-Api-Key`) for management endpoints; public redirects; rate limit by IP (+ key attribution).

## Reason
Enough to demonstrate authz boundaries without OIDC project scope.

## Evolution
Replace with Entra ID / OIDC; map `sub` to `ApiKeyId`/UserId column already present as string attribution field.
