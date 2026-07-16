---
name: review-observability
description: Review Planora structured logging, health checks, diagnostics, correlation, background/provider visibility, and sensitive-data hygiene. Use for logging or operational reviews; do not own API error responses.
---

# Review Observability

Ensure operators can detect, localize, and understand failures without leaking secrets or personal data.

## Inputs

- Service, provider, job, endpoint, or deployment flow in scope.
- Current logs, health checks, exception pipeline, and operational failure scenarios.
- Data sensitivity and production/runtime constraints.

## Boundaries

- Do not log passwords, tokens, cookies, keys, connection strings, reset/invite tokens, or unnecessary personal data.
- Do not use logging as a substitute for correct error handling or tests.
- Keep client/status behavior in `$review-error-handling`.

## Workflow

1. List critical operations, dependencies, background loops, and failure modes that require visibility.
2. Inspect log levels, structured properties, event context, exception capture, and duplicate/noisy logging.
3. Check whether logs distinguish user errors, authorization denials, provider outages, transient failures, and internal defects.
4. Review liveness/readiness semantics, database health, startup validation, and deployment probe expectations.
5. Check correlation across request, background job, email, storage, and data-cleanup flows where feasible.
6. Verify provider failures are visible while preserving user-action semantics.
7. Assess retention/cardinality/privacy risk and recommend the minimum useful telemetry.

## Verification

- Trigger safe representative failures and confirm useful structured output without sensitive values.
- Verify `/health/live` and `/health/ready` semantics when in scope.
- Inspect tests for provider/job logging behavior or document gaps.

## Outputs

- Observability map and blind spots.
- Severity-ranked logging/health findings and safe event fields.
- Validation evidence, privacy constraints, and operational follow-ups.

## Composition

- Use with `$planora-workflow`.
- Pair with `$review-error-handling` for request/client failure semantics.
- Add provider, container, Azure, or release reviews for operational context.
