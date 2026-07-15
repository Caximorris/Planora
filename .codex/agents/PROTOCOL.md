# Planora subagent protocol

This protocol defines how the root Codex agent coordinates the project-scoped agents in this
directory. `AGENTS.md` remains authoritative. Every Planora subagent uses `planora-workflow` plus
the narrow domain Skill named in its profile.

## Operating rules

1. The root agent owns the user objective, scope, approvals, task decomposition, and final answer.
2. A subagent receives one bounded question or deliverable. It does not broaden scope.
3. Read-only reviewers never edit. Implementers may edit only their assigned files and must preserve
   unrelated work. Parallel implementers must have disjoint file ownership.
4. Findings belong to the narrowest specialist. A general reviewer links to a specialist finding
   instead of restating it.
5. Agents do not spawn recursively. `max_depth = 1` keeps orchestration visible and predictable.
6. Cross-domain collaboration is a root-mediated handoff. The producing agent reports the exact
   assumption, artifact, or question the receiving specialist must evaluate.
7. No agent pushes, deploys, changes production resources, or crosses an `AGENTS.md` approval gate.

## Required handoff envelope

Every agent returns these fields, even when some are empty:

```text
STATUS: complete | blocked | no-findings
SCOPE: files, diff, flow, or command inspected
EVIDENCE: concrete observations and command results
OUTPUT: findings, decision, patch, or measurements
RISKS: residual uncertainty and unverified assumptions
VALIDATION: commands/checks run and exact outcomes
HANDOFFS: target agent -> bounded question or artifact
```

Reviewer findings use `P0` through `P3`, include a concrete failure path, and cite the tightest file
and line range. Reviewers return `no-findings` when they have no actionable defect.

## Ownership rule

- Root agent: orchestration and final synthesis only.
- Implementer: produces the scoped change.
- Reviewer: independently tries to disprove correctness in one risk domain.
- Investigator: establishes a reproducible cause or measured bottleneck; does not implement.
- Verifier: proves the claimed outcome after implementation; does not rediscover the bug.
- Release reviewer: aggregates evidence and issues a gate decision; does not repeat specialist work.
