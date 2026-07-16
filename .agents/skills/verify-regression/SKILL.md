---
name: verify-regression
description: Prove a reported Planora defect is fixed with a before/after reproduction and focused regression coverage. Use after a bug fix or risky correction; do not use as a substitute for initial diagnosis.
---

# Verify Regression

Demonstrate that the original failure no longer occurs, the test would catch its return, and adjacent behavior remains intact.

## Inputs

- Original symptom and confirmed root cause.
- Fix diff and focused regression test or reproducible manual flow.
- Relevant neighboring control cases.

## Boundaries

- Do not claim regression protection from a green unrelated suite.
- Do not weaken assertions to accommodate the fix.
- Keep performance and browser-only claims tied to the same environment used for baseline.

## Workflow

1. Reconstruct the exact original reproduction from evidence, not memory.
2. Confirm the regression test targets the root cause and would fail without the fix when practical.
3. Run the focused test or manual flow on the fixed code.
4. Run one or more adjacent controls that exercise the same boundary without the triggering condition.
5. Check authorization, validation, error, concurrency, responsive, or provider failure behavior when related.
6. Expand to affected build/test/format gates according to risk.
7. Record any environment branch that remains unverified, such as physical-device or production-provider behavior.

## Verification

- Show the original failure condition, post-fix result, and control result.
- Confirm the test name and assertion correspond to the defect.
- Inspect final diff/status for accidental changes.

## Outputs

- Before/after proof and regression test evidence.
- Adjacent controls and broader gates run.
- Residual uncertainty and conditions not reproduced.

## Composition

- Use with `$planora-workflow`.
- Normally follows `$investigate-bug` and an owning implementation skill.
- Add specialist review skills for the affected risk boundary.
