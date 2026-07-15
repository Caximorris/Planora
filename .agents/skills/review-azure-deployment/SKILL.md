---
name: review-azure-deployment
description: Review Planora Azure workflows, environment mapping, secrets, probes, rollout, and rollback. Use for Azure deployment configuration; do not deploy or change CI/CD without approval.
---

# Review Azure Deployment

Verify that repository configuration can produce a safe, diagnosable Azure rollout without exposing or mutating production resources.

## Inputs

- GitHub Actions workflows, target Azure services, environment-variable contract, and intended deployment change.
- Container image/runtime expectations and Static Web Apps build output.
- Secret names, domains, CORS, storage/email configuration, health probes, and rollback constraints.

## Boundaries

- Read secret names and mappings only; never request or print secret values.
- Do not run `az` mutations, push images, deploy, or edit CI/CD without required approval.
- Keep Docker build/runtime details in `$review-container`.

## Workflow

1. Map triggers, path filters, permissions, action versions, artifacts/images, and target resources.
2. Trace each application config key from code to workflow env mapping and secret reference.
3. Review API URL injection, CORS origins, web base URL, email/storage providers, registry credentials, and production-only settings.
4. Check liveness/readiness probe intent, scale-to-zero/cold start, migration-on-boot implications, and rollout ordering.
5. Assess least privilege, secret exposure in shell/logs, quoting, failure handling, concurrency, and rollback.
6. Compare workflow assumptions to current Azure service behavior using authoritative docs when material.
7. Classify changes requiring approval and provide a dry-run or non-mutating validation plan.

## Verification

- Validate YAML/shell syntax and repository path/output assumptions.
- Build the container and Web output locally where safe; do not emulate success for unavailable Azure state.
- Confirm exact environment key spelling against `Program.cs` and options classes.

## Outputs

- Deployment flow and configuration map.
- Findings, approval gates, rollout/rollback risks, and missing probes/config.
- Local validation and production-state unknowns.

## Composition

- Use with `$planora-workflow`.
- Pair with `$review-container`, `$review-release`, `$review-blob-storage`, and `$review-email-workflow`.
- Use `$investigate-ci` for failing Actions runs.
