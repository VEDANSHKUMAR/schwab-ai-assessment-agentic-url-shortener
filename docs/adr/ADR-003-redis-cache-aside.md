# ADR-003: Redis cache-aside

## Context
Redirect path must tolerate high read volume.

## Decision
Cache-aside key `url:{shortCode}` with TTL capped by expiry; invalidate on disable.

## Reason
Keeps PostgreSQL off the hot path; failure falls back to DB.

## Trade-offs
Possible brief staleness; stampede risk mitigated by short TTLs and unique keys (single-flight can be added later).
