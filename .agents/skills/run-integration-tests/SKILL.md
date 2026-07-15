---
name: run-integration-tests
description: Prepare, run, and diagnose Planora xUnit integration tests against the throwaway PostgreSQL database. Use for integration-suite execution or environment failures; do not use to design new coverage.
---

# Run Integration Tests

Run the real API test host safely and distinguish product failures from PostgreSQL, process-lock, and test-harness problems.

## Inputs

- Requested test scope or failing test name.
- Local PostgreSQL availability on port 5433.
- Current dev-server/process state and relevant test configuration.

## Boundaries

- Never point tests at production or a shared database.
- Stop live `dotnet watch` processes before build/test; do not kill unrelated user processes.
- Do not rewrite tests to hide an environment or product failure.

## Workflow

1. Inspect `PlanoraWebAppFactory`, the requested tests, and current process state.
2. Verify PostgreSQL is reachable on port 5433 and understand that the factory creates/drops `planora_test`.
3. Ensure no Planora API/Web watch process is locking assemblies or serving stale WASM assets.
4. Run the narrowest `dotnet test` filter first with useful verbosity.
5. Classify failures as setup/database, host configuration, test isolation, product behavior, or flaky timing.
6. Expand to the affected test area, then full `dotnet test Planora.slnx` when required.
7. Preserve complete decisive output and avoid rerunning unchanged failures without a new hypothesis.

## Verification

- Confirm the test process exit code and passed/failed/skipped counts.
- Re-run suspected flakes or isolation failures in a different order/filter.
- Report PostgreSQL or process prerequisites when they block validation.

## Outputs

- Exact commands, environment checks, and test results.
- Failure classification with first actionable cause.
- Next safe step and any tests not run.

## Composition

- Use with `$planora-workflow`.
- Use `$investigate-bug` for product failures and `$investigate-ci` when only CI fails.
- Use `$add-tests` only when coverage—not execution—is missing.
