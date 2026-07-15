---
name: review-ux-visuals
description: Review Planora UX consistency, hierarchy, design tokens, states, dark mode, motion, and polish. Use for design-system or visual review; do not review backend logic or accessibility.
---

# Review UX and Visuals

Keep the interface coherent with Planora’s Soft Glass Productivity UI while improving clarity rather than adding decoration.

## Inputs

- In-scope screens/components and intended user outcome.
- Rendered screenshots or runnable app.
- `docs/DESIGN_SYSTEM.md`, existing tokens, components, and state patterns.

## Boundaries

- Do not introduce a new design language, component system, or ad-hoc tokens.
- Glass remains an accent; content surfaces stay opaque as documented.
- Do not duplicate accessibility or responsive findings unless they directly explain visual inconsistency.

## Workflow

1. Compare the flow against the design system and neighboring established components.
2. Review hierarchy, density, spacing rhythm, typography, alignment, affordance, copy, and destructive-action clarity.
3. Check semantic color/token use, dark mode, priority/status consistency, borders, radii, shadows, and approved glass locations.
4. Verify loading, empty, error, disabled, selected, hover, focus-visible, and success feedback are visually coherent.
5. Check motion tokens, reduced-motion compatibility, sorted-list animation exclusions, and toast timing.
6. Review at representative desktop/mobile sizes but route layout defects to `$audit-responsive-ui`.
7. Rank changes by user clarity and consistency, not subjective novelty.

## Verification

- Inspect rendered light and dark modes and compare before/after screenshots when edits occur.
- Search for new raw colors, ad-hoc durations, and duplicated component styles.
- Confirm polish does not reduce readability, performance, or interaction stability.

## Outputs

- Consistency findings grouped by hierarchy, components, states, theme, and motion.
- Concrete token/component reuse recommendations.
- Rendered verification and intentionally deferred subjective preferences.

## Composition

- Use with `$planora-workflow`.
- Pair with `$review-blazor-ui` for behavior and `$audit-responsive-ui` for layout.
- Use `$audit-accessibility` for contrast, focus, semantics, and motion compliance.
