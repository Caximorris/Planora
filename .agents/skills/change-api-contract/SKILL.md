---
name: change-api-contract
description: Evolve Planora.Shared DTOs, enums, constants, serialization, and endpoint semantics across API and Web. Use for contract changes; do not use when the public contract is unchanged.
---

# Change API Contract

Keep the shared contract coherent, compatible, validated, documented, and compiled across both consumers.

## Inputs

- Current and proposed request/response shape or endpoint semantic.
- Compatibility expectations and known consumers.
- Validation, mapping, serialization, versioning, and migration implications.

## Boundaries

- `Planora.Shared` contains contracts only, never EF entities or infrastructure code.
- Do not silently rename, remove, reinterpret, or tighten fields when consumers or migration strategy are unclear.
- A public breaking change requires explicit approval.

## Workflow

1. Inventory the DTO/enum/constant, producer endpoints/mappers, Web services/components, tests, and docs.
2. Classify the change as additive-compatible, behavior-changing, or breaking.
3. Define nullability, optionality, defaults, enum behavior, validation, serialization names, and status semantics.
4. Update Shared first, then all API producers and Web consumers in the same change.
5. Use Mapperly conventions; add explicit mapping only for non-matching shapes.
6. Update create and partial-update validators together where applicable.
7. Add contract-focused tests for serialization/behavior and compatibility edge cases.
8. Update endpoint documentation and any architecture/session assumptions.

## Verification

- Build Planora.Shared, Planora.Api, and Planora.Web; then build/test the full solution.
- Search for stale field names, duplicated contracts, and unmapped consumers.
- Run format and diff gates; state any compatibility risk explicitly.

## Outputs

- Contract classification and compatibility decision.
- Synchronized Shared/API/Web/test/doc changes.
- Validation results, consumer inventory, and migration guidance if breaking.

## Composition

- Use with `$planora-workflow`.
- Usually compose with `$implement-endpoint` and/or `$implement-blazor-feature`.
- Use `$review-api-contract` as an independent verification pass for risky changes.
