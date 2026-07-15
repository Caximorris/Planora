---
name: implement-blazor-feature
description: Implement Planora Blazor WASM features through typed services, shared contracts, components, CSS, and JS interop. Use for Web behavior or UI flows; do not use for backend-only endpoints or review-only audits.
---

# Implement Blazor Feature

Deliver a frontend flow that respects Planora service boundaries, interaction invariants, responsive behavior, accessibility, and error states.

## Inputs

- User journey, routes/components, states, and acceptance behavior.
- Existing or required API contract and authorization behavior.
- Responsive, keyboard, visual, loading, empty, error, and unauthorized expectations.

## Boundaries

- Pages and components must not access EF Core, infrastructure, or raw production secrets.
- Centralize HTTP in one typed domain service; do not duplicate API calls in components.
- Preserve documented SortableJS, modal, fixed-overlay, search-input, dark-mode, and motion invariants.

## Workflow

1. Inspect neighboring pages/components, typed services, Shared DTOs, `app.css`, JS helpers, and design/mobile docs.
2. Model loading, success, empty, validation, error, unauthorized, and stale/concurrent states before coding.
3. Add or update the typed service first when server data is involved; compose `$change-api-contract` if Shared changes.
4. Keep reusable presentation in Components and avoid growing page-local business logic.
5. Use semantic tokens and existing motion patterns. Preserve `@key` on sortable lists and global modal conventions.
6. Implement keyboard semantics, focus behavior, labels, touch targets, and responsive layouts.
7. Verify rendered behavior at representative desktop/mobile sizes and dark/light/reduced-motion modes as applicable.
8. Restart dev servers for new routes or failed edit-and-continue; never build while watch processes are live.

## Verification

- Build Planora.Web; build the full solution when Shared changed.
- Run applicable tests and manual browser checks for every modeled state.
- Run format/diff/status gates and check console/network errors during rendered verification.

## Outputs

- UI, service, contract, style, and JS files changed.
- State model and interaction decisions.
- Build/test/browser evidence, accessibility/responsive checks, and remaining device-only uncertainty.

## Composition

- Use with `$planora-workflow`.
- Add `$review-blazor-ui`, `$audit-responsive-ui`, `$audit-accessibility`, and `$review-ux-visuals` proportionally.
- Use `$implement-endpoint` when the required backend capability does not exist.
