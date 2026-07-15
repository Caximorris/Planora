---
name: implement-endpoint
description: Implement or extend controller-based Planora API endpoints with DTOs, FluentValidation, authorization, mapping, docs, and tests. Use for endpoint work; do not use for review-only or frontend-only requests.
---

# Implement Endpoint

Deliver a complete API use case through the owning layers while preserving Planora contracts and workspace security.

## Inputs

- Endpoint behavior, caller, route, request and response shape, and status semantics.
- Authorization role or workspace membership rules.
- Persistence, side effects, compatibility constraints, and acceptance cases.

## Boundaries

- Controllers own HTTP concerns, not substantial business or persistence rules.
- Never trust request-body user IDs or return EF entities.
- Do not change public contracts, auth/security behavior, or destructive data semantics without required approval.

## Workflow

1. Inspect neighboring controller actions, Shared DTOs, validators, mappers, services, tests, and endpoint docs.
2. Define success and failure status codes, validation, authorization, idempotency, concurrency, and side effects.
3. Add or evolve `Planora.Shared` contracts, then add matching FluentValidation for every write request.
4. Enforce workspace membership and role before loading or mutating scoped resources; scope queries to prevent IDOR.
5. Keep the controller thin. Place use-case rules in Application services and provider/data details behind interfaces in Infrastructure.
6. Map entities to DTOs with Mapperly conventions and preserve media URL resolution.
7. Add focused integration tests for success, validation, unauthorized, forbidden/cross-workspace, not-found, conflict, and side effects as applicable.
8. Update `docs/api-endpoints.md` and `session.md` when behavior or architecture changes.

## Verification

- Build affected projects; build the full solution for Shared/API/Web changes.
- Run focused tests, then the full suite when contracts or cross-cutting behavior changed.
- Run `dotnet format Planora.slnx --verify-no-changes`, inspect diff/status, and re-exercise the endpoint.

## Outputs

- Endpoint and owning-layer files changed.
- Contract, authorization, validation, status, and side-effect decisions.
- Tests and commands run, docs updated, risks, and unresolved approval items.

## Composition

- Use with `$planora-workflow`.
- Add `$change-api-contract` when shared DTO or enum compatibility is material.
- Add `$review-authz`, `$review-error-handling`, `$review-ef-core`, or an integration review for the endpoint’s risks.
