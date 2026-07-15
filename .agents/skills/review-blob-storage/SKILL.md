---
name: review-blob-storage
description: Review Planora uploads, IFileStorage, private Azure Blob persistence, SAS reads, deletion, and legacy dual-read behavior. Use for storage changes; do not review generic Azure deployment.
---

# Review Blob Storage

Protect file safety, authorization, durability, URL confidentiality, and cleanup across local and Blob providers.

## Inputs

- Upload/download/delete flow, storage interfaces/providers, options, filter, controller, and tests.
- Allowed types, magic bytes, limits, ownership, stored URL shape, and migration/legacy needs.
- Azure container privacy and SAS lifetime assumptions.

## Boundaries

- Never trust filenames, extensions, MIME headers, paths, or client-provided URLs.
- Do not make the container public or expose connection strings/account keys.
- Changing upload policy, encryption, permissions, or production storage requires approval.

## Workflow

1. Trace authorization and workspace scoping before upload, read exposure, and delete.
2. Verify size and server-side magic-byte allowlists use shared limits where required.
3. Review server-generated names, path normalization, ownership parsing, content type, overwrite behavior, and cancellation.
4. Check private container creation, SAS generation/scope/lifetime, `MediaUrlResolutionFilter`, and no bypass of authorized API responses.
5. Review local/Blob provider parity, legacy relative URL dual-read, delete no-op guards, and permanent-delete cleanup.
6. Check failure atomicity between blob and database changes, orphan risks, retries, and provider outages.
7. Require focused unit/integration tests without real Azure resources unless explicitly provisioned.

## Verification

- Run storage, media URL, cover image, attachment, rate-limit, and workspace access tests as applicable.
- Test malicious filenames/content, oversize, cross-workspace access, foreign URLs, and cleanup failures.
- Confirm config contains no secret values and production assumptions are documented.

## Outputs

- Storage threat/data-flow map.
- Findings on validation, privacy, durability, URL resolution, and cleanup.
- Tests, approval needs, and provider/emulator gaps.

## Composition

- Use with `$planora-workflow`.
- Pair with `$review-authz`, `$audit-security`, `$review-error-handling`, and `$review-azure-deployment`.
- Use `$review-container` for ephemeral filesystem/runtime concerns.
