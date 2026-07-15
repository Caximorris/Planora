---
name: review-ef-core
description: Review Planora EF Core entities, configurations, queries, tracking, indexes, concurrency, transactions, and query filters. Use for data-access or model changes; do not review generated migration rollout here.
---

# Review EF Core

Protect data correctness and query performance while keeping persistence in Infrastructure and tenancy rules enforceable.

## Inputs

- Entities, configurations, DbContext, LINQ queries, services/controllers, and tests in scope.
- Expected cardinality, consistency, concurrency, and tenant boundaries.
- Generated SQL or performance evidence when available.

## Boundaries

- Do not hand-edit migration snapshots as an EF modeling shortcut.
- Do not recommend indexes without considering write cost and actual query shape.
- Do not rely on global query filters for authorization.

## Workflow

1. Map entity relationships, requiredness, ownership, cascades/restrict/set-null, indexes, and query filters.
2. Review LINQ for tenant scoping, N+1 queries, over-fetching, tracking needs, split/cartesian behavior, client evaluation, and pagination.
3. Check `SaveChangesAsync`, cancellation, transactions, atomic side effects, retries, and concurrency tokens/row versions.
4. Verify archive and soft-delete filters are consistent across normal, trash, restore, search, calendar, and provider cleanup paths.
5. Check workspace access before or within resource queries and preserve ordering indexes.
6. Inspect generated SQL or use focused benchmarks when query shape is material.
7. Require focused integration coverage for data invariants and concurrency.

## Verification

- Build Planora.Api and run affected integration/concurrency tests.
- Inspect migration output separately with `$review-migration` if the model changed.
- Confirm queries remain PostgreSQL/Npgsql compatible.

## Outputs

- Data model and query findings with concrete failure or cost.
- Recommended query/configuration correction and index rationale.
- Tests, SQL evidence, migration handoff, and residual risk.

## Composition

- Use with `$planora-workflow`.
- Pair model changes with `$review-migration`.
- Add `$review-authz`, `$investigate-performance`, or `$review-architecture` where relevant.
