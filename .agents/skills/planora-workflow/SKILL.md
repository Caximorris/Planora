---
name: planora-workflow
description: Apply Planora preflight, scope, approval, validation, and handoff gates. Use with every Planora implementation, fix, investigation, review, test, migration, or release task; do not use alone for domain work.
---

# Planora Workflow

Establish the repository-wide operating envelope. Pair this skill with the narrow skill that owns the requested work.

## Inputs

- The user objective and acceptance criteria.
- Current git status and any existing user changes.
- The owning project or files, relevant tests, and applicable repository documentation.

## Boundaries

- Do not replace a domain-specific implementation, investigation, or review skill.
- Do not modify production resources, secrets, CI/CD, security behavior, public contracts, or destructive migrations without the approval required by `AGENTS.md`.
- Treat `AGENTS.md` as authoritative when any skill guidance conflicts.

## Workflow

1. State the required model routing before substantive work. Stop only when the active model is known to be below the required tier; when the selector is unavailable, recommend the tier without claiming the current selection.
2. Read `AGENTS.md`, then the relevant parts of `CLAUDE.md`, `session.md`, and `docs/api-endpoints.md`. Read `references/repository-map.md` when locating an unfamiliar area.
3. Inspect `git status`, the current implementation, focused tests, and relevant project/workflow files. Preserve unrelated changes.
4. Classify the task as implementation, diagnosis, review, verification, or release work. Select the smallest owning skill plus any risk-specific review skills.
5. For defects, reproduce or establish an observable gap before editing. For changes, define scope, assumptions, risks, and validation first.
6. Make only in-scope changes. Stop at approval gates or when evidence cannot narrow the problem safely.
7. Use `references/verification-matrix.md` to select narrow-first validation. Inspect the final diff and status before handoff.

## Verification

- Confirm requested behavior or review objective is satisfied with direct evidence.
- Run applicable build, test, and format gates without live `dotnet watch` processes.
- Run `git diff --check`, inspect `git diff`, and confirm unrelated user changes remain untouched.

## Outputs

- A concise outcome-first summary.
- Files changed or reviewed, commands run, and results.
- Risks, approvals encountered, unresolved uncertainty, and any checks not run.

## Composition

- Always pair with the narrow skill that owns the task.
- Add `$review-authz` or `$audit-security` for identity, tenancy, upload, secret, or security-boundary work.
- Add `$review-api-contract`, `$review-migration`, or `$review-release` when those artifacts are in scope.
