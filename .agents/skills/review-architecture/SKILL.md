---
name: review-architecture
description: Assess Planora boundaries, ownership, dependency direction, coupling, and change blast radius. Use for designs, broad refactors, new subsystems, or misplaced logic; do not use for routine local code review.
---

# Review Architecture

Determine whether a proposal fits Planora’s Web/Shared/API/Application/Domain/Infrastructure boundaries and remains operable, testable, and reversible.

## Inputs

- Proposal, ADR, diff, or subsystem in scope.
- Business objective, constraints, expected growth, and compatibility needs.
- Current dependency graph, data ownership, and deployment model.

## Boundaries

- Do not recommend new abstractions solely for aesthetic purity.
- Do not expand a local task into a broad refactor without approval.
- Treat security, data integrity, deployment, and migration constraints as first-class architecture.

## Workflow

1. State the decision or architecture question and the forces that matter.
2. Map current components, dependencies, data flow, trust boundaries, and side effects.
3. Check ownership against repository boundaries and identify misplaced rules or provider coupling.
4. Evaluate alternatives for simplicity, change isolation, testability, operational cost, failure modes, and migration path.
5. Identify compatibility, rollout, rollback, concurrency, and data-loss implications.
6. Recommend the smallest architecture that satisfies current needs and names explicit extension seams.
7. Record rejected alternatives and the evidence or tradeoff that rejects them.

## Verification

- Trace at least one representative end-to-end flow through the proposed boundaries.
- Confirm the design can be tested with current infrastructure or state required additions.
- Check that the recommendation does not conflict with `AGENTS.md` approval/security rules.

## Outputs

- Current-state and proposed boundary map.
- Decision, alternatives, tradeoffs, risks, and migration sequence.
- Required tests, docs, approvals, and unresolved assumptions.

## Composition

- Use with `$planora-workflow`.
- Add `$review-authz`, `$review-ef-core`, `$review-migration`, `$review-azure-deployment`, or provider reviews as needed.
- Use `$review-code` only when a concrete diff also needs defect review.
