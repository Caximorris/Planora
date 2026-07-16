---
name: review-dependencies
description: Review Planora NuGet, Actions, container-image, and vendored dependency changes for need, security, license, and compatibility. Use for dependency changes; do not perform broad code review.
---

# Review Dependencies

Accept only justified dependency changes with a controlled compatibility and supply-chain story.

## Inputs

- Dependency diff or candidate package/action/image/vendor update.
- Reason for change, current version, transitive graph, and affected projects.
- Security advisory, license, release notes, or compatibility evidence.

## Boundaries

- Do not upgrade unrelated dependencies opportunistically.
- Do not modify vendored SortableJS without a deliberate separately reviewed upgrade.
- Significant security, licensing, or compatibility impact requires approval.

## Workflow

1. Inventory direct and material transitive changes across csproj files, Actions, Docker images, and vendored assets.
2. Confirm the dependency is necessary and built-in/existing alternatives are insufficient.
3. Check source/reputation, maintenance, license, advisories, pinned version strategy, and release notes.
4. Evaluate .NET 10, ASP.NET Core, EF/Npgsql, Blazor WASM, test, runtime, and deployment compatibility.
5. Review API surface, transitive bloat, startup/bundle impact, analyzer/build behavior, and rollback.
6. Run restore/build/test and dependency-specific checks; do not suppress warnings.
7. Document upgrade rationale, migration steps, and future ownership.

## Verification

- Run `dotnet restore`, applicable build/tests, and inspect warnings.
- Verify workflow action and container-image changes against authoritative release sources.
- Check lock/pin behavior and final diff for accidental broad updates.

## Outputs

- Dependency inventory and risk classification.
- Recommendation: accept, reject, pin differently, or stage.
- Validation, advisories/licenses checked, rollback, and approval needs.

## Composition

- Use with `$planora-workflow`.
- Add `$audit-security` for advisory-driven changes and `$investigate-ci` for restore/build failures.
- Use `$review-container` or `$review-azure-deployment` for runtime/deploy dependencies.
