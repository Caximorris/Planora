---
name: review-blazor-ui
description: Review Planora Blazor component correctness, state, rendering, services, JS interop, drag/drop, modals, and failure states. Use for UI code review; do not perform full responsive, accessibility, or visual audits.
---

# Review Blazor UI

Find interaction and lifecycle defects specific to Blazor WASM and Planora’s established frontend patterns.

## Inputs

- Changed or targeted Razor components, services, CSS, and JS.
- Expected user flow and state transitions.
- Rendered evidence, console/network output, and relevant API contracts.

## Boundaries

- Keep responsive, accessibility, and visual-system deep dives in their dedicated skills.
- Do not move backend rules into components or call raw `HttpClient` outside typed services.
- Preserve Planora hot reload, route restart, SortableJS, modal, overlay, search, dark-mode, and motion invariants.

## Workflow

1. Trace data from typed service through component state to rendered output and user events.
2. Review loading, empty, success, validation, error, unauthorized, stale, and concurrent states.
3. Check lifecycle methods, async cancellation/disposal, duplicate requests, event propagation, and navigation.
4. Verify `@key` on card/column/board sorted loops and idempotent SortableJS initialization in applicable renders.
5. Check global `.modal.d-block` plus `.btn-close` behavior and fixed-overlay stacking-context constraints.
6. Inspect JS interop names, timing, idempotency, cleanup, and console failure behavior.
7. Render the affected flow and exercise keyboard/mouse/touch-adjacent interactions before reporting.

## Verification

- Build Planora.Web; build all consumers when Shared changed.
- Inspect browser console/network and reproduce each finding in rendered UI when feasible.
- Route responsive, accessibility, and visual concerns to their specialist audits without duplication.

## Outputs

- Severity-ranked Blazor/interaction findings.
- State/lifecycle/data-flow evidence and correction direction.
- Build/browser checks and specialist follow-ups.

## Composition

- Use with `$planora-workflow` and usually `$review-code`.
- Add `$audit-responsive-ui`, `$audit-accessibility`, or `$review-ux-visuals` for those dimensions.
- Use `$review-api-contract` when service and DTO expectations differ.
