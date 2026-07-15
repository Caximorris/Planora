---
name: investigate-performance
description: Measure and locate Planora latency, throughput, allocation, query, rendering, or startup bottlenecks. Use before optimization when performance is unclear; do not use to implement unmeasured tuning.
---

# Investigate Performance

Establish a baseline, isolate the dominant cost, and produce a ranked optimization hypothesis backed by measurements.

## Inputs

- Slow operation, endpoint, page, query, test, or deployment symptom.
- Representative data size, environment, concurrency, and target budget.
- Available traces, timings, SQL, browser evidence, or resource metrics.

## Boundaries

- Do not optimize from intuition alone.
- Do not use production load or production data without explicit authorization.
- Keep database, API, Web, network, and cold-start costs separated.

## Workflow

1. Define the user-visible metric and an acceptable target.
2. Build a reproducible representative scenario and record the baseline.
3. Measure at the correct layer: browser/rendering, HTTP, application, EF Core/SQL, storage/email provider, or container startup.
4. Narrow the bottleneck with controlled experiments; change one factor at a time.
5. Inspect query shape, round trips, serialization, allocations, polling, JS interop, rendering churn, and cold-start effects as applicable.
6. Rank candidates by expected impact, confidence, risk, and implementation cost.
7. Recommend the smallest experiment that can validate the top candidate.

## Verification

- Repeat measurements enough to expose variance and warm/cold differences.
- Compare against a control and preserve raw commands or trace locations.
- Confirm the suspected bottleneck accounts for a material share of total time.

## Outputs

- Scenario, environment, baseline, and target.
- Evidence-backed bottleneck analysis and ranked hypotheses.
- Recommended optimization experiment and measurement plan.

## Composition

- Use with `$planora-workflow`.
- Use `$review-ef-core` for query/model causes and `$review-blazor-ui` for rendering causes.
- Use `$optimize-performance` only after a bottleneck is measured.
