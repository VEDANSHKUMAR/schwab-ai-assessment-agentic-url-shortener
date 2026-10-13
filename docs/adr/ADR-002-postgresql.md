# ADR-002: PostgreSQL as primary database

## Context
Need durable storage with strong uniqueness guarantees for short codes/aliases.

## Decision
PostgreSQL + EF Core.

## Reason
Relational constraints prevent alias races; rich indexing; Azure-friendly.

## Trade-offs
Operational overhead vs document DB simplicity — uniqueness and analytics queries favor SQL.
