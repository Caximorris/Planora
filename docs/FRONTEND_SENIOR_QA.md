# Planora Frontend Senior QA - 2026-07-07

Scope: `Planora.Web` layout, navigation, responsive CSS, modals, search, notifications, workspaces, board/kanban, calendar, profile, and SortableJS interop.

## Summary

This pass found 0 P0, 4 P1, 9 P2, and 4 P3 issues.

Implemented fixes focused on correctness and polish, not redesign: valid workspace nav markup, safer mobile viewport sizing, contained modal/search scrolling, keyboard access for clickable rows/cards/tiles, accessible labels for icon-only controls, long-text containment, and protection against filtered card drag/drop corrupting hidden card order.

## Issues Found By Category

### P0

None found.

### P1

1. Invalid nested workspace nav buttons.
   - Files: `Planora.Web/Pages/Workspaces.razor`, `Planora.Web/wwwroot/css/app.css`
   - Problem: each workspace select button contained a delete button, which is invalid HTML and can produce inconsistent click, focus, and screen-reader behavior.
   - Status: fixed by replacing the nested delete button with a sibling button inside `.ws-nav-row`.

2. Filtered kanban drag/drop could persist the wrong card order.
   - Files: `Planora.Web/Components/KanbanColumn.razor`, `Planora.Web/wwwroot/js/board-sortable.js`, `Planora.Web/wwwroot/css/app.css`
   - Problem: when a priority filter is active, SortableJS reports indexes for the visible subset, while `Board.razor` persists positions against the full hidden+visible card list.
   - Status: fixed by disabling card SortableJS while a filter is active. Column reorder remains enabled.

3. Mobile viewport sizing relied on `100vh`.
   - Files: `Planora.Web/wwwroot/css/app.css`, `Planora.Web/Pages/Landing.razor.css`, `Planora.Web/Components/SearchModal.razor.css`
   - Problem: mobile browser chrome can make `100vh` taller than the usable viewport, hiding modal/search content or bottom-nav-adjacent content.
   - Status: fixed with `--viewport-h` using `100dvh` when supported and fallback `100vh`.

4. Touch users could not discover critical hover-only actions.
   - Files: `Planora.Web/wwwroot/css/app.css`
   - Problem: workspace delete, board archive, column settings, comment delete, and member remove controls were opacity-hidden until hover, which does not exist on coarse pointers.
   - Status: fixed by exposing those controls under `(hover: none), (pointer: coarse)` and on keyboard focus.

### P2

1. Workspace mobile nav intended to scroll horizontally but `.ws-nav` was not a flex container.
   - Status: fixed with mobile `display:flex`, row items, and horizontal overflow containment.

2. Clickable kanban cards, board tiles, and notification rows were not keyboard reachable.
   - Status: fixed with `role="button"`, `tabindex="0"`, Enter/Space handlers, and visible focus states.

3. Many icon-only buttons lacked accessible names.
   - Status: fixed for close buttons, settings buttons, delete/dismiss buttons, board archive, and calendar previous/next.

4. Modals could exceed small viewport height.
   - Status: fixed with viewport-bounded `.modal-dialog`, flex `.modal-content`, and scrolling `.modal-body`.

5. Search command palette could exceed mobile viewport height.
   - Status: fixed with viewport-bounded container/results and a tighter mobile top offset.

6. Long names and freeform text could break layout.
   - Status: fixed for board title, kanban card text, labels, checklists, comments, members, notifications, profile email/name, and archived card rows.

7. Calendar grid risked body-level horizontal overflow at 320px.
   - Status: fixed by containing horizontal scroll inside `.calendar-root` and setting a stable grid min-width.

8. z-index values were mixed literals and tokens.
   - Status: partially fixed by moving nav, dropdown, search, and modal layers onto existing tokens.

9. Disabled controls had inconsistent pointer feedback.
   - Status: fixed with a shared disabled cursor rule.

### P3

1. Unused template scoped CSS still exists in `Layout/NavMenu.razor.css` and `Layout/MainLayout.razor.css`.
   - Deferred: dead-file cleanup is reviewable but unrelated to this QA pass.

2. Most Bootstrap-style modals still do not trap focus or close on Escape.
   - Deferred: needs a shared modal behavior rather than per-modal one-offs.

3. Board tile reorder still uses native HTML5 drag/drop, which is weak on touch.
   - Deferred: fixing this well requires either SortableJS for board tiles or explicit move controls.

4. Form labels are visually present but not consistently associated with inputs via `for`/`id`.
   - Deferred: broad markup cleanup across auth/profile/workspace/card forms.

## Files Involved

Created:
- `docs/FRONTEND_SENIOR_QA.md`

Modified:
- `Planora.Web/Components/KanbanCard.razor`
- `Planora.Web/Components/KanbanColumn.razor`
- `Planora.Web/Components/SearchModal.razor.css`
- `Planora.Web/Layout/MainLayout.razor`
- `Planora.Web/Pages/Board.razor`
- `Planora.Web/Pages/Calendar.razor`
- `Planora.Web/Pages/Landing.razor.css`
- `Planora.Web/Pages/Notifications.razor`
- `Planora.Web/Pages/Workspaces.razor`
- `Planora.Web/wwwroot/css/app.css`
- `Planora.Web/wwwroot/js/board-sortable.js`

## Fixes Implemented

- Replaced nested workspace delete buttons with valid sibling controls.
- Added keyboard activation to kanban cards, board tiles, notification dropdown rows, and notification page rows.
- Added `aria-label`, `aria-pressed`, `role="dialog"`, and `aria-modal` where low-risk and immediately useful.
- Added stable dynamic viewport sizing through `--viewport-h`.
- Bounded modal and search overlays to the viewport with internal scrolling.
- Exposed hover-only micro-actions on touch devices and keyboard focus.
- Added wrapping/min-width constraints for long board/card/workspace/member/comment/checklist/label/notification text.
- Contained calendar overflow inside the calendar page.
- Disabled SortableJS card reordering while a priority filter is active.
- Moved several layered UI surfaces to existing z-index tokens.

## Fixes Deferred

- Full modal focus trap and Escape handling for every modal.
- Touch-native board tile reordering.
- Associated form labels across all forms.
- Dead scoped CSS deletion for unused template files.
- Drag/drop rollback UI on failed persistence.
- Notification service lifecycle hardening and polling visibility pause.

## Manual Testing Checklist

- 320px mobile
- 390px mobile
- mobile landscape
- tablet
- desktop
- desktop wide
- browser zoom 125%
- browser zoom 150%
- light mode
- dark mode
- login/register
- logout
- protected route redirect
- workspace list
- board page
- board horizontal scroll
- column reorder
- card reorder same column
- card move across columns
- card modal
- comments
- checklists
- labels
- assignees
- priority filter
- Ctrl+K search
- notifications dropdown
- calendar
- profile
- invite flow
- archive/unarchive
- empty states
- loading states
- error states

## Known Risks

- Card drag is intentionally disabled under priority filters; users must clear the filter before reordering cards.
- Keyboard activation on draggable cards/tiles improves accessibility but should be browser-tested with SortableJS to confirm it does not affect pointer drag behavior.
- Modal focus trapping remains incomplete, so keyboard users can still tab behind non-search modals.
- Existing modified files outside this pass were preserved and not reverted.
