---
name: audit-responsive-ui
description: Audit Planora layouts and interactions across mobile, tablet, desktop, orientation, touch, and safe areas. Use for responsive or mobile QA; do not use for general Blazor correctness or accessibility-only audits.
---

# Audit Responsive UI

Verify that every in-scope flow remains reachable, readable, scrollable, and operable across representative viewport and input conditions.

## Inputs

- Routes/components and target device classes.
- Current rendered app or runnable local services.
- Known breakpoints, mobile audit history, and expected interaction behavior.

## Boundaries

- Do not infer rendered layout from CSS alone when the app can be run.
- Do not reopen historical findings marked resolved without current evidence.
- Keep semantic accessibility and visual-brand judgments in their focused reviews.

## Workflow

1. Inventory in-scope routes, overlays, tables/lists, forms, navigation, and drag interactions.
2. Inspect CSS breakpoints and layout tokens, then test at 390x844, 320x568, 844x390, 667x375, 768px boundary, and representative desktop sizes as applicable.
3. Measure page and contained overflow, clipped content, fixed-nav clearance, safe-area handling, modal fit, keyboard/input zoom risk, and touch target reachability.
4. Exercise portrait/landscape, coarse-pointer drag/scroll behavior, bottom sheets, calendar mobile mode, filter collapse, and long-content cases.
5. Test loading, empty, error, unauthorized, and dense-data states.
6. Capture screenshots or computed measurements for every reported defect.
7. Separate emulator evidence from physical-device-only claims.

## Verification

- Confirm no unintended page-level horizontal overflow and all primary actions remain reachable.
- Retest the exact failing viewport after fixes and one adjacent breakpoint.
- Check light/dark modes and browser console when relevant.

## Outputs

- Viewport matrix and pass/fail inventory.
- Severity-ranked findings with screenshots or measurements.
- Fix direction, verified resolutions, and physical-device gaps.

## Composition

- Use with `$planora-workflow`.
- Pair with `$review-blazor-ui` for component causes and `$review-ux-visuals` for polish.
- Add `$audit-accessibility` when responsive changes affect focus, semantics, or targets.
