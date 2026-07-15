---
name: add-tests
description: Add focused Planora unit or PostgreSQL-backed integration tests for behavior, regressions, security, validation, and infrastructure. Use when coverage must be created; do not use merely to run existing tests.
---

# Add Tests

Create deterministic coverage at the lowest layer that still proves the behavior and its important failure modes.

## Inputs

- Behavior or defect to prove, relevant implementation, and acceptance cases.
- Existing nearby tests, fixtures, and test infrastructure.
- Required authorization, data, provider, concurrency, or failure conditions.

## Boundaries

- Do not write tests that assert implementation trivia instead of behavior.
- Do not use shared production databases, external email, or real Azure resources.
- bUnit is not configured; adding it is a separate explicit feature, not an incidental dependency.

## Workflow

1. Choose pure unit tests for deterministic logic and `WebApplicationFactory<Program>` integration tests for API/auth/data behavior.
2. Follow existing test organization and the shared `Integration` collection.
3. Create unique test data; do not depend on test order or global resets between cases.
4. Cover the happy path plus validation, unauthorized/forbidden cross-workspace, not-found/conflict, and provider failure paths as relevant.
5. Use existing helpers and fakes such as auth helpers and capturing email sender.
6. For regression work, make the test fail for the original reason before applying the fix when practical.
7. Keep assertions precise enough to distinguish the target failure from setup failure.

## Verification

- Run the narrow test filter repeatedly, then the affected test project or full suite.
- Confirm PostgreSQL uses the throwaway `planora_test` database on port 5433.
- Inspect for timing dependence, leaked state, real network calls, and brittle exact-message assertions.

## Outputs

- Tests added and behavior matrix covered.
- Why the selected test level is appropriate.
- Commands/results, setup requirements, and uncovered risk.

## Composition

- Use with `$planora-workflow`.
- Use `$run-integration-tests` for environment setup and suite execution.
- Use `$verify-regression` when proving a specific bug fix.
