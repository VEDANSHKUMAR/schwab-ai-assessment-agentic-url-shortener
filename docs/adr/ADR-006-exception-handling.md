# ADR-006: Centralized exception handling

## Decision
Middleware mapping domain/validation exceptions to RFC 7807 ProblemDetails.

## Reason
Consistent API errors; controllers stay thin; no stack traces in production responses.
