# ADR-004: Short-code generation

## Options
1. DB sequence → Base62 (compact, enumerable)
2. Random Base62
3. Cryptographically secure random Base62
4. Hash of URL

## Decision
Cryptographically secure random Base62 (length 8) + DB unique constraint + bounded retry. Custom aliases are explicit opt-in.

## Reason
Avoids leaking creation volume/order; custom aliases stay first-class; hash-based codes hurt distinct campaigns to same URL.

## Trade-offs
Slightly longer codes than sequential; collision retries rare at 62^8.
