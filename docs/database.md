# Database

PostgreSQL tables (EF Core migrations under `src/UrlShortener.Infrastructure/Persistence/Migrations`):

## urls

| Column | Notes |
|--------|-------|
| Id | UUID PK |
| ShortCode | Unique index — correctness under concurrency |
| OriginalUrl | Max 2048 |
| CreatedAt / UpdatedAt | timestamptz |
| ExpiresAt | nullable |
| IsActive | soft delete |
| ApiKeyId | optional attribution |
| IdempotencyKey | unique filtered index |
| ClickCount | denormalized counter updated by analytics worker |

Index `(IsActive, ExpiresAt)` supports operational queries.

## click_events

Append-only analytics. Index `(ShortCode, OccurredAt)` for time-range aggregates.
