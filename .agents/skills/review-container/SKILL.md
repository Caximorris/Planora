---
name: review-container
description: Review the Planora API Dockerfile and Compose/container runtime for build reproducibility, security, size, ports, health, config, and persistence. Use for container concerns; do not review Azure rollout or deploy.
---

# Review Container

Ensure the API image builds predictably and runs with correct least-privilege, configuration, networking, health, and storage assumptions.

## Inputs

- `Planora.Api/Dockerfile`, build context, Compose/hosting files, and intended runtime.
- Target architecture, port, environment, filesystem, and dependency needs.
- Build output, image metadata, vulnerability results, or startup logs when available.

## Boundaries

- Do not change deployment workflows or production registry state.
- Do not embed credentials or rely on writable ephemeral storage for durable uploads.
- Do not upgrade base images casually; route material dependency changes to review.

## Workflow

1. Inspect stages, base images, SDK/runtime versions, restore layering, copy scope, build configuration, publish output, entrypoint, and port.
2. Check `.dockerignore`, deterministic restore/build, cache behavior, final image contents, and unnecessary tooling.
3. Review user privileges, filesystem permissions, read-only feasibility, temp/upload paths, certificates, signals, shutdown, and background service behavior.
4. Trace runtime env configuration, database connectivity, static files, health endpoints, and `PORT` behavior.
5. Review Compose differences from test/dev documented ports and persistence volumes.
6. Build and run locally when safe, inspect logs/health, and test graceful startup/shutdown.
7. Assess image/security scan findings and rollback/version tagging.

## Verification

- Run `docker build` and a minimal container start/health check when tooling is available.
- Inspect final image history/size/user/ports without exposing config secrets.
- Confirm local container validation does not imply Azure configuration correctness.

## Outputs

- Container build/runtime findings.
- Reproducibility, security, persistence, and health assessment.
- Commands/results, image assumptions, and Azure handoff.

## Composition

- Use with `$planora-workflow`.
- Pair with `$review-azure-deployment`, `$review-dependencies`, and `$review-release`.
- Use `$review-blob-storage` for upload persistence semantics.
