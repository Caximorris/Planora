---
name: audit-accessibility
description: Audit Planora keyboard access, focus, semantics, labels, contrast, motion, and assistive-technology behavior. Use for accessibility review or regressions; do not use as a visual-polish or responsive-only audit.
---

# Audit Accessibility

Find barriers in complete user flows and verify both automated signals and manual keyboard/focus behavior.

## Inputs

- In-scope routes, components, and user journeys.
- Rendered app plus relevant Razor/HTML/CSS/JS.
- Target standard or severity expectations when specified.

## Boundaries

- Automated audits are evidence, not the entire accessibility review.
- Do not claim screen-reader support without inspecting semantics and, when required, testing with assistive technology.
- Keep purely visual consistency and layout breakpoint issues in their specialist skills.

## Workflow

1. Inventory interactive controls, headings/landmarks, forms, dialogs, dynamic status, drag alternatives, and content order.
2. Run an automated accessibility audit when available and inspect source semantics.
3. Navigate the flow keyboard-only: tab order, visible focus, activation, escape, focus trap/restore, and no keyboard trap.
4. Check labels/name/role/value, `aria-expanded`, live/status messaging, validation association, and icon-only controls.
5. Check contrast in light/dark themes, reduced motion, zoom/reflow, touch target minimums, and non-color cues.
6. Verify modals inherit global accessibility behavior and sorted/drag content has keyboard alternatives where expected.
7. Report exact user barrier, affected users, and a semantic correction.

## Verification

- Re-run automated checks and manual keyboard flow after changes.
- Verify focus starts and returns correctly for dialogs/sheets/search.
- State any screen-reader, physical device, or browser coverage not performed.

## Outputs

- Flow-based accessibility checklist and severity-ranked barriers.
- Source/rendered evidence and recommended semantic fix.
- Automated/manual validation and residual coverage gaps.

## Composition

- Use with `$planora-workflow`.
- Pair with `$review-blazor-ui` and `$audit-responsive-ui` for implementation/layout causes.
- Use `$review-ux-visuals` only for non-accessibility design consistency.
