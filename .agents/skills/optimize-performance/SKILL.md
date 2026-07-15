---
name: optimize-performance
description: Implement a measured Planora performance improvement without changing behavior. Use after a bottleneck and baseline are established; do not use for exploratory diagnosis or architecture rewrites without evidence.
---

# Optimize Performance

Convert a proven bottleneck into a narrow, reversible improvement with before-and-after evidence.

## Inputs

- Measured baseline, target, dominant bottleneck, and reproduction scenario.
- Relevant implementation and correctness tests.
- Constraints on caching, consistency, memory, database load, or UX.

## Boundaries

- Do not proceed without a repeatable baseline.
- Do not trade authorization, validation, consistency, or correctness for speed.
- Do not add caches, indexes, batching, or concurrency without reviewing invalidation, write cost, and failure modes.

## Workflow

1. Confirm the baseline and bottleneck still reproduce.
2. Select the smallest change with a plausible material impact.
3. Add or preserve correctness coverage before changing behavior-sensitive code.
4. Implement within the owning layer and keep contracts stable unless explicitly approved.
5. Measure the same scenario under the same conditions.
6. Check secondary costs: allocations, DB writes, contention, stale data, bundle size, complexity, and cold starts.
7. Retain the change only if evidence meets the target or materially improves the dominant cost.

## Verification

- Report before/after values, variance, environment, and command or trace method.
- Run focused correctness tests plus affected build and format gates.
- Inspect the diff for accidental semantic, security, or contract changes.

## Outputs

- Implemented optimization and rationale.
- Before/after measurements and target status.
- Correctness evidence, tradeoffs, rollback path, and residual bottlenecks.

## Composition

- Use with `$planora-workflow` and the owning implementation skill.
- Add `$review-ef-core`, `$review-blazor-ui`, `$review-container`, or `$review-azure-deployment` for the affected layer.
- Use `$verify-regression` for correctness and performance regression proof.
