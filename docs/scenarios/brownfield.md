# Scenario: Brownfield

**Brief:** Harden custom-alias uniqueness and cache invalidation on disable.

**Path:** Requirements → ImpactAnalysis → Architecture (gate) → Impl → Tests → Docs → Release (gate).

**Validation:** Impact artifact lists modules/tests; disable path already invalidates Redis in product code.
