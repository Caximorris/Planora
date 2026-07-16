---
name: review-error-handling
description: Review Planora exceptions, HTTP failure semantics, Web service errors, user feedback, retries, and cleanup. Use for failure paths; do not use for logging-only or general contract review.
---

# Review Error Handling

Make failures predictable, safe, actionable, and consistent across API, typed services, and Blazor UI.

## Inputs

- Endpoint or UI flow and its expected failures.
- Exception middleware, controller results, typed service behavior, toasts/forms, and tests.
- Retry/idempotency and provider-side-effect constraints.

## Boundaries

- Do not expose stack traces, secrets, provider internals, or cross-tenant resource existence.
- Do not retry non-idempotent operations blindly.
- Keep field/DTO compatibility in `$review-api-contract` and telemetry in `$review-observability`.

## Workflow

1. Enumerate validation, unauthenticated, forbidden, not-found, conflict/concurrency, rate-limit, provider, network, cancellation, and unexpected failures.
2. Trace each failure from source to API status/payload, typed service parsing, component state, and user feedback.
3. Check exception middleware, status consistency, ProblemDetails or local payload conventions, and environment-specific disclosure.
4. Verify retries, timeout/cancellation, idempotency, partial side effects, cleanup, and transaction boundaries.
5. Check expired-session redirect/refresh behavior and avoid toast storms or swallowed errors.
6. Require focused tests for material failure paths and recovery behavior.
7. Recommend one consistent correction per root semantic mismatch.

## Verification

- Exercise representative failures and inspect status, body, UI state, and logs.
- Confirm successful behavior and side effects remain unchanged.
- Check unauthorized/cross-workspace cases for information leakage.

## Outputs

- Failure matrix from source to user.
- Findings with expected versus actual semantics.
- Tests, correction direction, and residual provider/network uncertainty.

## Composition

- Use with `$planora-workflow`.
- Pair with `$review-api-contract`, `$review-authz`, and `$review-observability` as needed.
- Use provider-specific review skills for email/storage/notification semantics.
