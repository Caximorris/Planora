---
name: investigate-ci
description: Diagnose failing or flaky Planora CI and GitHub Actions checks from logs, workflows, and local reproduction. Use for CI failures; do not use for deployment changes or local-only tests.
---

# Investigate CI

Locate the first actionable CI failure, distinguish workflow defects from code defects, and propose the smallest safe correction.

## Inputs

- Repository and branch or PR, failing check name, run URL, or captured logs.
- Relevant workflow YAML, project files, and recent diff.
- Local environment differences and secret/service availability.

## Boundaries

- Do not edit workflows when the failure is in product code.
- Do not expose secrets or print secret values while debugging.
- Changing CI/CD requires the approval defined in `AGENTS.md`; diagnosis is read-only by default.

## Workflow

1. Retrieve check status and logs with the installed GitHub workflow skill or `gh` when available; otherwise use supplied logs.
2. Identify the earliest failing step and ignore downstream cancellations or cascades.
3. Map the CI command, SDK, service container, paths, permissions, and environment to repository expectations.
4. Reproduce the exact command locally when safe, matching Release configuration and `--no-restore` or `--no-build` semantics.
5. Classify the cause: code/test, dependency restore, PostgreSQL service, workflow syntax, permissions, secret/config, flaky timing, or runner/tool drift.
6. Propose the smallest fix and state whether approval is required before editing workflow files.
7. After an approved fix, re-run the narrow local command and inspect workflow syntax/diff.

## Verification

- Cite the failing step and decisive log lines without dumping irrelevant logs.
- Confirm local reproduction or explain the environment gap.
- Do not claim success until the required GitHub check reruns green or clearly state that remote rerun is pending.

## Outputs

- Failing check, first root error, and classification.
- Local reproduction evidence and smallest fix.
- Approval needs, validation results, and residual flake risk.

## Composition

- Use with `$planora-workflow`.
- Compose with the installed `gh-fix-ci` skill when live PR checks are available.
- Use `$run-integration-tests`, `$review-dependencies`, or `$review-container` for the classified cause.
