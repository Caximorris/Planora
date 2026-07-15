---
name: review-api-contract
description: Validate Planora endpoint routes, Shared DTOs, validation, mapping, serialization, status codes, docs, and Web consumers. Use for contract audits or diffs; do not implement intentional contract changes.
---

# Review API Contract

Prove that API producers and consumers agree and that compatibility and failure semantics are explicit.

## Inputs

- Endpoint or changed contract surface.
- Shared DTOs/enums/constants, controllers, mappers, validators, Web services, and docs.
- Compatibility expectations and external consumers when known.

## Boundaries

- Do not treat internal EF entities as public contracts.
- Do not infer compatibility from successful API compilation alone.
- Route intentional implementation to `$change-api-contract`.

## Workflow

1. Inventory route/method, request/response types, query parameters, content types, and status codes.
2. Trace every contract field through Shared, API validation/mapping, Web deserialization/use, tests, and documentation.
3. Check nullability, optional updates, defaults, enum evolution, date/time, row versions, media URLs, and error payloads.
4. Classify each change as additive-compatible, semantic, or breaking.
5. Find stale consumers, duplicate DTOs, missing validators, Mapperly gaps, undocumented behavior, and mismatched status handling.
6. Verify unauthorized, forbidden, validation, not-found, conflict, and provider failure semantics where applicable.
7. Recommend a synchronized correction and migration strategy for any incompatibility.

## Verification

- Build or inspect compile results for Shared, API, and Web.
- Search for every changed field/route and compare tests/docs.
- Use focused serialization or endpoint tests when static inspection is insufficient.

## Outputs

- Contract inventory and compatibility classification.
- Findings with producer/consumer evidence.
- Required synchronized changes, tests, docs, and approval needs.

## Composition

- Use with `$planora-workflow`.
- Use `$change-api-contract` to implement accepted changes.
- Add `$review-authz` and `$review-error-handling` for security and failure semantics.
