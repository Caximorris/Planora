---
name: investigate-bug
description: Reproduce Planora bugs, regressions, intermittent failures, or unclear behavior and isolate the root cause. Use for defect investigation; do not use for features or speculative cleanup.
---

# Investigate Bug

Turn an ambiguous symptom into a reproducible failure, an owning layer, and the smallest justified fix direction. Diagnosis is the deliverable unless the user also asks for implementation.

## Inputs

- Observed versus expected behavior, error text, screenshots, logs, or failing tests.
- Environment, route or endpoint, user role, and reproduction conditions when known.
- Relevant source, tests, recent diffs, and runtime output.

## Boundaries

- Do not edit code when the request is diagnosis-only.
- Do not infer root cause from correlation, a single log line, or stale documentation.
- Stop when required credentials, external services, or data state are unavailable after safe local checks.

## Workflow

1. Restate the symptom as a falsifiable failure condition.
2. Inspect git status, likely entry points, tests, recent related changes, and runtime logs.
3. Reproduce with the narrowest safe method: focused test, API request, local UI flow, or deterministic code path.
4. Trace data and control flow across Web, Shared, API, and Infrastructure only as far as evidence requires.
5. Form competing hypotheses and eliminate them with targeted observations.
6. Identify the first incorrect state transition, violated invariant, or boundary mismatch. Separate trigger, root cause, and downstream symptoms.
7. If implementation is requested, hand the confirmed cause to the owning implementation skill and add a regression test first when practical.

## Verification

- Re-run the original reproduction and a nearby control case.
- Confirm the proposed fix point explains all observed evidence without requiring unrelated changes.
- Record any nondeterminism, environment dependency, or unverified branch.

## Outputs

- Reproduction steps and evidence.
- Root cause, owning layer, and causal chain.
- Smallest safe fix direction, regression-test target, and remaining uncertainty.

## Composition

- Use with `$planora-workflow`.
- After diagnosis, compose with `$implement-endpoint`, `$implement-blazor-feature`, `$change-api-contract`, or `$optimize-performance` as appropriate.
- Use `$verify-regression` to prove the implemented fix.
