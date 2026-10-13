# ADR-001: Modular monolith vs microservices

## Context
Need a deliverable that is runnable, testable, and interview-defensible within a short window, while leaving extraction paths open.

## Options
1. Microservices (API, redirect, analytics)
2. Modular monolith
3. Single project dump

## Decision
Modular monolith with bounded modules (Api/Application/Domain/Infrastructure/Orchestration).

## Reason
Operational complexity of microservices is not justified at this stage; module boundaries preserve future split of redirect vs management.

## Trade-offs
Simpler deploy vs eventual need to scale redirect independently.

## Consequences
One deployable process today; extract Redirect + Analytics worker later if traffic demands.
