# Security

- **API keys** on create/read/delete/analytics management routes
- **Rate limiting** on create and redirect (per IP)
- **FluentValidation**: http(s) only, no userinfo, blocked domains, private/loopback/metadata host rejection
- **SSRF**: we do **not** server-fetch destinations. Storing/redirecting is browser navigation (different threat model). Private-host rejection is defense-in-depth against abuse.
- **Soft delete** preserves audit history
- **ProblemDetails** without stack traces in production
- **Secrets** via configuration/env — never commit production keys

Extension points: domain allow/block lists, reputation checks, OIDC replacement for API keys.
