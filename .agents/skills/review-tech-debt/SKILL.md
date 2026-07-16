---
name: review-tech-debt
description: Identify and prioritize Planora technical debt by recurring cost, risk, reach, and remediation value. Use for debt audits; do not label preferences or roadmap features as debt.
---

# Review Technical Debt

Produce a ranked, bounded debt backlog tied to operational or development pain instead of a generic refactor wish list.

## Inputs

- Scope, time horizon, known pain points, incidents, TODOs, audits, and roadmap context.
- Code complexity, duplication, test gaps, stale docs, build friction, and ownership evidence.
- Constraints on behavior changes and refactor breadth.

## Boundaries

- Do not treat unbuilt product features as technical debt.
- Do not recommend broad rewrites without repeated cost or material risk.
- Separate current debt from historical findings already resolved in docs.

## Workflow

1. Define the review scope and debt criteria: recurring toil, defect risk, security/data exposure, performance, operability, testability, or obsolete complexity.
2. Gather evidence from source, tests, CI, docs, TODOs, change history, and known incidents.
3. Group symptoms by root cause to avoid duplicate backlog items.
4. Score each item by impact, frequency, reach, confidence, effort, and dependency order.
5. Identify quick containment, durable remediation, and what should intentionally remain.
6. Build a sequence that pays down enabling debt before dependent cleanup.
7. Attach acceptance evidence and a stop condition to every recommended item.

## Verification

- Confirm each item still exists in current code and is not marked resolved.
- Trace claimed cost to concrete files, failures, or repeated workflow friction.
- Review the backlog for duplicates, speculative future-proofing, and hidden feature work.

## Outputs

- Ranked debt register with evidence and scores.
- Recommended sequence, containment, remediation, and acceptance criteria.
- Deferred or rejected items with reasons.

## Composition

- Use with `$planora-workflow`.
- Use specialist review skills to validate high-risk debt items.
- Use `$review-architecture` only when remediation crosses boundaries.
