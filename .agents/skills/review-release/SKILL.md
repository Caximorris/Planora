---
name: review-release
description: Assess Planora release readiness across scope, tests, contracts, migrations, dependencies, security, operations, deployment, and rollback. Use before merge or release; do not deploy, push, or merge.
---

# Review Release

Produce an evidence-based go, conditional-go, or no-go decision with explicit blockers and rollback requirements.

## Inputs

- Release diff or commit range, target environment, and intended changes.
- CI/build/test results, migrations, dependency changes, docs, and deployment configuration.
- Known incidents, rollout window, monitoring, and rollback capability.

## Boundaries

- Do not infer readiness from a green build alone.
- Do not push, merge, deploy, or modify production resources without approval.
- Unknown critical validation, destructive migration risk, or security regression is a blocker.

## Workflow

1. Freeze the exact release scope and identify unrelated or missing changes.
2. Map affected contracts, data, auth/security, providers, UI, dependencies, CI/CD, and runtime configuration.
3. Verify required build/test/format gates and focused behavior evidence.
4. Review migration order/data safety, configuration/secret names, container/runtime, Azure workflows, health checks, and provider readiness.
5. Check documentation, compatibility, monitoring signals, rollback/forward-fix plan, and ownership.
6. Classify findings as blockers, required conditions, accepted risks, or follow-ups.
7. Issue a go, conditional-go, or no-go decision tied to evidence.

## Verification

- Inspect final diff, diff-check, status, and CI results for the exact commit range.
- Confirm all required specialist reviews are complete for touched risks.
- State every skipped check and whether it blocks release.

## Outputs

- Decision and release scope.
- Blockers, conditions, accepted risks, rollback, and monitoring checklist.
- Validation matrix and post-release verification plan.

## Composition

- Use with `$planora-workflow`.
- Compose with `$review-migration`, `$review-dependencies`, `$review-authz`, `$audit-security`, `$review-container`, and `$review-azure-deployment` based on scope.
- Use `$verify-regression` for critical fixes included in the release.
