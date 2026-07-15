---
name: review-migration
description: Review generated Planora EF Core migrations and model snapshot for PostgreSQL rollout, data preservation, locking, rollback, and compatibility. Use for migration artifacts; do not design general EF queries here.
---

# Review Migration

Determine whether a migration can be applied safely and whether it needs approval, backfill, staged rollout, or rollback planning.

## Inputs

- Generated migration, designer, snapshot diff, model change, and deployment context.
- Existing data assumptions, table size, nullability, defaults, and compatibility window.
- Rollback and rollout constraints.

## Boundaries

- Do not hand-edit existing historical migrations or snapshots.
- Do not apply destructive or irreversible migrations without explicit approval and a preservation plan.
- Do not assume an empty development database represents production.

## Workflow

1. Confirm the migration was generated after a clean Planora.Api build and matches the intended model change.
2. Inspect `Up`, `Down`, designer, and snapshot together.
3. Classify operations: additive, backfill, constraint/index, rename, type conversion, rewrite, destructive, or irreversible.
4. Evaluate existing-row behavior, defaults, null transitions, FK/cascade changes, index build cost, locks, table rewrites, and app-version compatibility.
5. Check PostgreSQL/Npgsql semantics and whether a staged expand/backfill/contract sequence is needed.
6. Define rollback or forward-fix strategy and data-preservation verification.
7. Require migration-focused tests or a disposable database apply/rollback inspection proportional to risk.

## Verification

- Build Planora.Api and inspect generated SQL when risk is non-trivial.
- Apply only to a disposable/local test database when authorized; never shared or production data.
- Run relevant integration tests and compare snapshot/model consistency.

## Outputs

- Safety classification and approval requirement.
- Operation-by-operation risks, rollout sequence, and rollback/data plan.
- Validation performed and unresolved production assumptions.

## Composition

- Use with `$planora-workflow`.
- Pair with `$review-ef-core` for model/query semantics and `$review-azure-deployment` for rollout mechanics.
- Use `$review-release` when migration ordering gates a release.
