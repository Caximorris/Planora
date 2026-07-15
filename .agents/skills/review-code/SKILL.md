---
name: review-code
description: Review a Planora diff, branch, commit, or PR for actionable correctness, security, regression, and maintainability defects. Use for general code review; do not use as the sole review for specialist risk areas.
---

# Review Code

Find defects that matter, rank them by impact, and route touched risk surfaces to focused review skills. A review reports findings; it does not implement changes unless asked.

## Inputs

- Diff, commit range, branch, PR, or changed-file set.
- User intent and acceptance criteria.
- Relevant source, tests, docs, and validation output.

## Boundaries

- Prioritize defects over style preferences and summaries.
- Do not report hypothetical issues without a concrete failure path or violated invariant.
- Do not edit, comment on GitHub, or resolve threads unless explicitly requested.

## Workflow

1. Read `AGENTS.md`, inspect git status, and establish the exact review range.
2. Understand intended behavior from the request, docs, tests, and neighboring code.
3. Map changed files to architecture and specialist risks: contracts, auth, EF/migrations, UI, providers, deployment, or dependencies.
4. Review behavior, authorization, validation, concurrency, error paths, data integrity, resource cleanup, and test adequacy.
5. Run focused read-only checks or tests when they materially confirm a finding.
6. Rank findings P0-P3 with precise file/line locations and user impact.
7. If no findings survive verification, say so and list residual test or environment gaps.

## Verification

- Trace each finding to a reproducible condition or concrete invariant.
- Check whether existing tests already cover or contradict the concern.
- Keep line ranges tight and avoid duplicate findings with the same root cause.

## Outputs

- Actionable findings first, ordered by severity.
- For each finding: location, failure scenario, impact, and correction direction.
- Residual risks and validation gaps; brief summary only after findings.

## Composition

- Use with `$planora-workflow`.
- Compose with every specialist review implied by touched files; do not duplicate their findings.
- Use installed GitHub review skills only for retrieving or addressing live PR threads.
