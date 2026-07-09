# Planora — Mobile Views Audit

> **Status: RESOLVED (2026-07-09).** All findings below were fixed and verified live — see
> [Section 8](#8-resolution-shipped-2026-07-09). Sections 1–7 are preserved as the original
> pre-fix audit; the 6/10 score and severities in them describe the state *before* the fixes.

**Date:** 2026-07-09
**Method:** Static code audit (Razor + `app.css` + JS interop). Dev servers were stopped for an
unrelated build, so this pass is **code-based, not live-rendered**. Every finding that needs a real
device/emulator to confirm severity is tagged **[verify]**. Nothing here was confirmed against
running pixels — treat layout-overflow claims as high-confidence-but-unverified.
**Scope covered:** all 14 routes, the layout shell, both Kanban components, the drag JS, and the
shared stylesheet (2082 lines).

---

## 1. Executive Summary

**Overall mobile readiness: 6 / 10 — usable, with real friction on the core surface (the board).**

This is *not* a desktop-only app bolted onto a phone. Whoever built it already did the hard,
frequently-skipped things:

- A dedicated **bottom tab bar** replaces the desktop icon rail at ≤768px (`app.css:1708-1743`).
- `--viewport-h` uses `100dvh` behind an `@supports` check (`app.css:118, 165-167`) — the correct
  fix for the iOS URL-bar `100vh` bug.
- `@media (hover: none), (pointer: coarse)` reveals hover-only action buttons on touch
  (`app.css:1904-1912`).
- `prefers-reduced-motion` is honored (`app.css:1936-1943`); `:focus-visible` rings are defined
  broadly (`app.css:1916-1934`); the live search is debounced (`Board.razor:1028-1041`); icon-only
  buttons mostly carry `aria-label`.

**Biggest risks (what keeps the score at 6, not 8):**

1. **P0 — Touch drag vs. scroll conflict on the board.** SortableJS is initialized with **no
   `delayOnTouchOnly` / `touchStartThreshold`**, and card lists have **no drag handle** — the whole
   card is draggable (`board-sortable.js:36-59`). On a touchscreen, dragging a finger to scroll a
   column vertically (or the board horizontally) will grab a card instead. This degrades the app's
   primary surface. **[verify]** exact feel on-device.
2. **P1 — `viewport-fit=cover` is missing** from the viewport meta (`index.html:6`). The whole
   stylesheet leans on `env(safe-area-inset-*)`, but without `viewport-fit=cover` those values
   resolve to **0 on notched iOS**, so the fixed bottom nav sits in the home-indicator zone and all
   the safe-area padding is dead code.
3. **P1 — Calendar is unreachable from the mobile nav.** The Calendar link is
   `side-nav-item--rail-only`, which is `display:none` at ≤768px (`app.css:1722`). It is not in the
   bottom bar and nothing deep-links to it on mobile → the feature is orphaned on phones.
4. **P2 (broad) — iOS input zoom everywhere.** `html` is `14px` (`app.css:201`); Bootstrap
   `.form-control`/`.form-select` are `1rem` = 14px, and board filter inputs are 12-13px
   (`app.css:695, 850`). Every input focus on iOS Safari zooms the page (needs ≥16px to suppress).

**Does it feel usable on mobile today?** For *reading* boards, navigating, auth, and editing cards
via the modal — yes, it will feel deliberate and decent. For the signature Kanban *interaction*
(drag to move cards) and for anything on the Calendar — no. Those are the two places a senior
reviewer would call fragile.

---

## 2. Route-by-Route Audit

| Route | File | Status | Main issue(s) | Breakpoints | Severity |
|---|---|---|---|---|---|
| `/` Landing | `Landing.razor` | **Needs work** *(verify)* | Marketing page not inspected in depth; auth-style gradient shell is responsive. Confirm hero/CTA stacking. | 320-430 | P2 |
| `/login` | `Login.razor` | **Good** | Inputs 14px → iOS zoom. Card is `max-width:400px; width:100%` — fine. | all | P2 |
| `/register` | `Register.razor` | **Good** *(verify)* | Same input-zoom; assumed same `.auth-card`. | all | P2 |
| `/forgot-password` | `ForgotPassword.razor` | **Good** *(verify)* | Same shell. | all | P3 |
| `/reset-password` | `ResetPassword.razor` | **Good** *(verify)* | Same shell. | all | P3 |
| `/confirm-email` | `ConfirmEmail.razor` | **Good** *(verify)* | Status page, low risk. | all | P3 |
| `/invite/{token}` | `InviteAccept.razor` | **Needs work** *(verify)* | `.invite-form`/`.invite-link-row` go column at ≤480 (`app.css:1897`) — check role `<select>` + link copy row width. | 320-375 | P2 |
| `/home` | `Home.razor` | **Good** | `home-grid` `minmax(160px,1fr)` at ≤768 (`app.css:1779`). Two cards/row at 375. | all | P3 |
| `/workspaces` | `Workspaces.razor` | **Needs work** | Sidebar becomes a horizontal scroll strip (`app.css:1750-1769`); board tiles use **HTML5 native DnD** (per CLAUDE.md) which has **no touch support** → tile reordering likely dead on mobile. **[verify]** | all | P2 |
| `/workspaces/{id}/settings` | `WorkspaceSettings.razor` | **Needs work** *(verify)* | Member rows, invite form, role selects — check wrapping + touch targets on remove buttons. | 320-430 | P2 |
| `/boards/{id}` | `Board.razor` | **Broken (interaction)** | Drag/scroll conflict (P0); board chrome low-contrast text; filter panel eats vertical space; column max-height cramped in landscape. | all | **P0** |
| `/calendar` | `Calendar.razor` | **Broken** | Unreachable from mobile nav (P1); grid `min-width:620px` forces horizontal scroll of the whole page on phones (`app.css:1830`). | all | **P1** |
| `/notifications` | `Notifications.razor` | **Good** | `max-width:700px; margin:0 auto`, item rows use `min-width:0` + `overflow-wrap` (`app.css:1865-1888`). Bottom padding reserved for nav. | all | P3 |
| `/profile` | `Profile.razor` | **Good** | Rows wrap, inputs go full-width at ≤768 (`app.css:1781-1785`). | all | P3 |

> Routes tagged *(verify)* were confirmed to exist and share known-responsive shells, but their
> specific markup was not line-read in this pass.

---

## 3. Component-Level Audit

### Navigation / header / bottom bar (`MainLayout.razor`, `app.css:1708-1743`)
- **Good:** rail→bottom-bar transform; 52px touch targets (`app.css:1725`); rail dropdowns become
  fixed bottom-sheets above the bar with a backdrop to dismiss (`app.css:1738-1743`); active-route
  indicator repositioned for horizontal layout.
- **Issues:** Calendar dropped from bottom bar (P1); bottom bar relies on dead `env(safe-area-inset-bottom)`
  (P1, see viewport-fit); notification/account dropdowns are not focus-trapped.

### Board layout (`Board.razor`, `app.css:620-961`)
- **Good:** `board-columns` is `overflow-x:auto` with `overscroll-behavior-x: contain`
  (`app.css:928-932`); columns are `flex-shrink:0; width:280px`; drag auto-disables when filters are
  active (`data-dnd-disabled`, `Board.razor:244`).
- **Issues:** 280px column on a 320px viewport leaves only ~24px peek of the next column → horizontal
  scroll is **not obvious** (P3, verify); `board-header` low-contrast chrome text
  (`rgba(226,232,240,0.45)`, `app.css:638,666`) likely fails WCAG AA (P2); the whole filter shell
  wraps and can consume most of a short landscape viewport (P2, verify).

### Cards (`KanbanCard.razor`, `app.css:1062-1127`)
- **Good:** `role="button"`, `tabindex=0`, keyboard `Enter/Space` open, `aria-label` with title;
  labels capped at `Take(5)`; `overflow-wrap:anywhere` on title; single assignee avatar (no overflow
  from many assignees).
- **Issues:** entire card is the SortableJS drag target with no handle (feeds the P0 conflict); tap
  target is the whole card (fine) but tap-vs-drag disambiguation is unguarded.

### Filters (`Board.razor:37-238`, `app.css:665-926`)
- **Good:** quick-filter pills become a horizontal scroll row on mobile (`app.css:1792-1793`); advanced
  panel grid collapses to 1 column (`app.css:1795`); `type="search"` input is uncontrolled per project
  rule; `aria-pressed` on toggle pills; active-filter chips are removable.
- **Issues:** `.filter-pill` ≈22px tall (padding `3px 11px`, font 11px, `app.css:711-713`) — below the
  24px WCAG 2.5.8 minimum (P2); `.filter-control` at 12px → iOS zoom (P2); filter panel is tall on
  small screens (P3).

### Modals / dialogs (`app.css:1129-1149`; `Board.razor`, `KanbanColumn.razor`)
- **Good:** `.modal-content` is `max-height: calc(var(--viewport-h) - 1rem)` with internal scroll and
  `overflow:hidden` (`app.css:1136-1142`) → no modal taller than the screen; `modal-dialog` margin
  tightened at ≤480 (`app.css:1894`); `aria-modal`, `aria-labelledby`, backdrop-click-to-close present
  on every dialog.
- **Issues (all dialogs — card detail, column settings, board settings, activity, delete confirms,
  crop):** **no focus trap, no focus-move-on-open, no Escape-to-close** at the dialog level (only a few
  inline text inputs handle Escape, e.g. `Board.razor:1688`, `KanbanColumn.razor:161,192`). P2 a11y.
  Card-detail modal is content-heavy (title/desc/assignee/due/priority/color/labels/checklists/comments)
  — scroll handles height, but the `.row g-2` due-date + priority split into `col-6/col-6`
  (`Board.razor:319-333`) is tight at 320px (P3, verify).

### Forms / inputs (Login/Register/Profile/WorkspaceSettings, card modal)
- **Good:** native `type="date"` (`Board.razor:322`), `type="email"`/`type="password"` on auth;
  labels present; buttons full-width where appropriate (`btn w-100`).
- **Issues:** universal 14px input font → iOS zoom (P2); `autocomplete`/`autocorrect` not tuned on the
  card-title textarea (P3, verify).

### Buttons / dropdowns / menus
- **Issues:** `.settings-gear-btn--sm` is 20×20 (`app.css:997-998`) — below 24px (P2); Bootstrap
  `.btn-close` in modal headers is small (P3); dropdowns rely on backdrop tap to close (works on
  touch, but no Escape).

### Tables / lists
- Calendar month grid is the only true "table" — `min-width:620px` inside `overflow-x:auto`
  (`app.css:1819,1830`). Horizontal-scrolls the page on phones (P2). Notification/member/activity lists
  are flex rows with `min-width:0` guards — good.

### Loading / empty / error states
- **Good:** board + calendar spinners; `ErrorBoundary` with Retry in `MainLayout` (`MainLayout.razor:166-174`);
  `#blazor-error-ui` bar; toasts reposition above the bottom nav at ≤640 (`app.css:2002-2007`); shared
  `EmptyState` component; filtered-empty column shows "No matching cards" (`KanbanColumn.razor:25-28`).
- **Issues:** a genuinely empty column (no filter) shows only the "+ Add a card" button, no empty
  affordance (P3, minor). Broken cover-image / avatar fallbacks not verified (P3, verify).

---

## 4. Prioritized Issue List

### P0 — blocks core mobile usage

**P0-1 — Card drag conflicts with scroll & tap on touch**
- **Location:** `wwwroot/js/board-sortable.js:36-59` (`planoraInitCardLists`), interacts with
  `Components/KanbanCard.razor` (whole card draggable).
- **Description:** SortableJS is created without `delayOnTouchOnly`, `delay`, or `touchStartThreshold`,
  and card lists define no `handle`. A touch-drag anywhere on a card starts a Sortable drag, so
  vertical column scroll and horizontal board scroll both fight the drag engine; tap-to-open is also
  ambiguous with a small finger movement.
- **Repro:** On a real phone, open a board with a column taller than the screen and try to scroll the
  column by dragging over a card. **[verify]**
- **Fix:** Add `delayOnTouchOnly: true`, `delay: 180`, `touchStartThreshold: 8` to both Sortable
  configs; consider a dedicated drag handle on cards for touch (a grip icon) so tap≠drag. Keep desktop
  behavior unchanged (delay only applies to touch).
- **Complexity:** M · **Risk:** Medium — Sortable option changes can subtly alter desktop feel; test
  both. Card-list and column configs must stay consistent.

### P1 — major UX/layout issue

**P1-1 — `viewport-fit=cover` missing → all safe-area insets are 0 on iOS**
- **Location:** `wwwroot/index.html:6`.
- **Description:** `<meta name="viewport" content="width=device-width, initial-scale=1.0">` lacks
  `viewport-fit=cover`. Every `env(safe-area-inset-*)` (`app.css:1718,1734,1741,1800`, etc.) resolves to
  0, so the fixed bottom nav overlaps the home-indicator gesture area on notched iPhones and toasts
  sit too low.
- **Repro:** iPhone 12/13/14/15 Safari, portrait — bottom tab labels crowd the home indicator. **[verify]**
- **Fix:** append `, viewport-fit=cover` to the meta content.
- **Complexity:** S · **Risk:** Low.

**P1-2 — Calendar orphaned on mobile navigation**
- **Location:** `Layout/MainLayout.razor:36-42` (`side-nav-item--rail-only`) + `app.css:1722`.
- **Description:** Calendar is hidden from the bottom bar and appears nowhere else on mobile; only a
  typed URL reaches it.
- **Repro:** ≤768px — no Calendar entry point exists.
- **Fix:** either include Calendar in the bottom bar (5-6 tabs is fine), or add a Calendar entry to the
  workspace/board context on mobile, or surface it in the account dropdown.
- **Complexity:** S-M · **Risk:** Low-Medium — the bottom bar already has 5 items; adding a 6th needs a
  width/label check at 320px.

**P1-3 — Calendar grid unusable width on phones**
- **Location:** `app.css:1819` (`.calendar-root { overflow-x:auto }`) + `app.css:1827-1831`
  (`.calendar-grid { min-width:620px }`).
- **Description:** A 7-column month grid forced to 620px means the whole page horizontally scrolls on
  any phone; day cells are cramped.
- **Repro:** 320-430px — horizontal scroll of the month view.
- **Fix:** below ~600px, switch to a mobile calendar pattern (agenda/list of upcoming dated cards, or a
  vertical week view) rather than a fixed-width month grid.
- **Complexity:** L · **Risk:** Medium — new layout branch; keep desktop grid intact.

### P2 — noticeable but not blocking

**P2-1 — App-wide iOS input zoom (font < 16px).** `app.css:201` (html 14px) + Bootstrap `.form-control`
1rem + `app.css:695` (13px search) + `app.css:850` (12px filter controls). *Fix:* set inputs/selects/
textareas to `font-size:16px` at `(max-width:768px)` (or `(pointer:coarse)`). Complexity S · Risk Low
(visual size bump).

**P2-2 — Modal a11y: no focus trap / focus-on-open / Escape.** All dialogs in `Board.razor`,
`KanbanColumn.razor`. *Fix:* shared modal wrapper that moves focus in on open, traps Tab, closes on
Escape, restores focus on close. Complexity M · Risk Medium (touches every modal).

**P2-3 — Low-contrast board chrome text.** `app.css:638,666,699` (`rgba(226,232,240,0.45-0.48)` on
translucent dark). Likely < 4.5:1. *Fix:* raise opacity/darken backdrop. Complexity S · Risk Low.

**P2-4 — Touch targets below 24px.** `.filter-pill` ≈22px (`app.css:711-713`), `.settings-gear-btn--sm`
20px (`app.css:997`). *Fix:* min 24px (ideally 44px) hit area on coarse pointers. Complexity S · Risk Low.

**P2-5 — Landscape column height cramped.** `app.css:1801` — `calc(var(--viewport-h) - bottom-nav - 104px)`
leaves very short columns in phone landscape (~211px at 375h). *Fix:* reduce reserved chrome in
landscape / allow taller columns. Complexity S · Risk Low. **[verify]**

**P2-6 — Workspace board-tile reorder has no touch support.** `Workspaces.razor` uses HTML5 native DnD
(per CLAUDE.md). Native DnD does not fire on touch. *Fix:* SortableJS or up/down controls on mobile, or
accept view-only ordering on touch. Complexity M · Risk Medium. **[verify]**

### P3 — polish

- **P3-1** Horizontal-scroll discoverability: 280px column leaves ~24px peek at 320px (`app.css:952`).
  Consider a slightly narrower mobile column (e.g. 82vw) so the next column peeks. Risk Low. **[verify]**
- **P3-2** No in-board back button on mobile (breadcrumb hidden, `app.css:1788`); users rely on the
  bottom "Spaces" tab. Consider a back chevron in `board-header`. Risk Low.
- **P3-3** Empty (unfiltered) column shows no empty-state affordance. Risk Low.
- **P3-4** Card-title textarea `autocapitalize`/`autocorrect` not tuned (`KanbanColumn.razor:34-38`). Risk Low.
- **P3-5** `col-6/col-6` due-date+priority row tight at 320px (`Board.razor:319-333`). Risk Low. **[verify]**

---

## 5. Recommended Implementation Plan

**Phase 1 — Critical mobile blockers**
- P0-1 SortableJS touch options (+ optional card drag handle).
- P1-1 `viewport-fit=cover`.
- P1-2 Calendar reachable from mobile nav.

**Phase 2 — Core UX**
- P1-3 Mobile calendar layout (agenda/week).
- P2-1 16px inputs (kills iOS zoom).
- P2-5 Landscape column height.
- P2-6 Workspace tile reorder on touch.

**Phase 3 — Accessibility & contrast**
- P2-2 Modal focus trap / Escape / focus-on-open (shared wrapper).
- P2-3 Board chrome contrast.
- P2-4 Touch-target sizes.

**Phase 4 — Polish**
- P3-1..P3-5 (scroll affordance, back button, empty column, input attrs, tight rows).

---

## 6. Mobile QA Checklist (manual)

Run each at **320 / 375 / 390 / 430 / 768px**, **portrait + landscape**, on **iOS Safari** and
**Android Chrome**. Real device for at least iOS (safe-area + input-zoom are Safari-specific).

**Auth**
- [ ] Login/Register/Forgot/Reset cards centered, no horizontal scroll, buttons full-width.
- [ ] Focusing email/password does **not** zoom the page (after P2-1).
- [ ] 2FA code entry usable; recovery-code grid wraps.

**Navigation**
- [ ] Bottom bar reachable, not under the home indicator (after P1-1).
- [ ] Every primary section (Home, Spaces, Search, Alerts, Account, **Calendar**) reachable (after P1-2).
- [ ] Notification & account bottom-sheets open, scroll, and dismiss via backdrop.
- [ ] Active tab indicator correct per route.

**Board usage**
- [ ] Horizontal column scroll is discoverable and smooth; `overscroll` contained.
- [ ] Scrolling a tall column vertically does **not** grab a card (after P0-1).
- [ ] Dragging a card to another column works via handle/long-press (after P0-1).
- [ ] Long titles, 5 labels, due date + priority + checklist badge don't break card layout.
- [ ] Board header actions (Activity, settings gear) tappable.

**Card create/edit**
- [ ] "+ Add a card" textarea usable; Enter submits, Shift+Enter newline.
- [ ] Card modal scrolls internally, never exceeds viewport; date picker is native.
- [ ] Assignee/priority selects usable; color swatches tappable.

**Filters**
- [ ] Quick-filter pills scroll horizontally; pills ≥24px tall (after P2-4).
- [ ] Advanced panel collapses to one column; checkboxes tappable.
- [ ] Active-filter chips removable; "Clear all" works.

**Modals**
- [ ] Escape closes; focus moves in on open and is trapped (after P2-2).
- [ ] Backdrop tap closes; no background scroll bleed.

**Forms**
- [ ] All inputs ≥16px (no zoom); labels visible; validation messages readable.

**Empty / error / loading**
- [ ] Empty board/column, no-results-after-filter, failed request (toast), expired session (redirect),
  permission-denied, missing board all render sanely.

**Calendar**
- [ ] Reachable on mobile; month/agenda view readable without full-page horizontal scroll (after P1-3).

---

---

## 7. Live Verification (real rendered pass)

**Tooling:** Chrome DevTools (via MCP) driving the running app (`Web:5076` + `Api:8080`, Postgres
local), emulated as mobile with touch at **390×844, 320×568, 844×390 landscape, 667×375 landscape**.
Authenticated session (user "Crop Tester", "Showcase Board" with 4 columns / 8 cards). Measurements
are computed rects / live JS object inspection, not eyeballed. This section **supersedes** the
code-only severities where they differ.

### What the live pass CONFIRMED (hard evidence)

| Finding | Evidence (measured) | Verdict |
|---|---|---|
| **P0-1 drag/scroll conflict** | Live `cardsList._planoraSortable.options` = `{delay:0, delayOnTouchOnly:false, touchStartThreshold:3, handle:null}`. A 3px touch move = drag; scrolling needs >3px. | **CONFIRMED.** Latent with 2 cards/col (no scroll needed); guaranteed once a column exceeds viewport height. |
| **P1-1 safe-area dead** | `env(safe-area-inset-bottom)` computes to **`0px`** on the running page. Meta lacks `viewport-fit=cover`. | **CONFIRMED at CSS level.** Home-indicator overlap itself needs a physical notched device (emulator can't inject the inset). |
| **P2-1 iOS input zoom** | Board search **13px**, filter controls **12px**, calendar workspace select **14px**, card-modal inputs **13px** — all `< 16px`. | **CONFIRMED**, app-wide. |
| **P2-2 modal a11y** | On card open, `document.activeElement` = the `.kanban-card` behind the modal (**focus never enters**). `body { overflow: visible }` → **background scrolls behind the modal**. | **CONFIRMED + expanded** (scroll-bleed is a new sub-finding). |
| **P2-4 touch targets** | Column gear `.settings-gear-btn--sm` = **20×20px** (< 24px WCAG 2.5.8). Filter pill = **24×24px** (meets floor, not comfortable). | **CONFIRMED** for gear; pill **downgraded** (it meets 24px). |
| **Filter chrome height** | `.board-header` = **179px** portrait = **21%** of 844h / **31%** of 320×568 / **46%** of 667×375 landscape. | **ELEVATED to P2** (was P2/P3) — see LV-1. |

### What the live pass CHANGED or DISPROVED (corrections)

- **No page-level horizontal overflow anywhere.** `documentElement.scrollWidth - innerWidth = 0` at
  320, 390, and both landscape sizes. The board and calendar scroll **inside contained regions**, not
  the page. My code-audit worry about "cards/containers wider than viewport" → **not reproduced**; the
  `min-width:0` / `overflow-wrap:anywhere` discipline holds up. Good.
- **P1-2 "Calendar unreachable" → downgraded to P2.** Calendar is absent from the bottom nav (true),
  but the **Home dashboard has a "Calendar" card link** (verified, uid on `/home`). It is reachable,
  just not from primary nav. Severity **P2 discoverability**, not P1 inaccessible.
- **P1-3 calendar grid → refined (still real).** Grid renders **620px inside a 390px** viewport →
  cells **34px wide**; only **Mon–Thu are visible, Fri/Sat/Sun are off-screen with no scroll hint**
  (screenshot captured). But it's a **contained** scroll (`.calendar-root{overflow-x:auto}`), so the
  page doesn't break. Keep as **P1** on UX grounds (half the week hidden is worse than "cramped").
- **P3-1 scroll discoverability → confirmed width-dependent.** Next-column peek = **94px at 390px**
  (clearly discoverable) but only **24px at 320px**. Fine on modern phones, marginal on an SE.

### New finding from the live pass

**LV-1 (P2, was implicit) — Board filter chrome does not collapse and dominates short viewports.**
- **Evidence:** header 173–179px regardless of orientation. In **667×375 landscape** that is **46% of
  the screen**, leaving a **127px** card area, and the column's `bottom` (399px) **overlaps the bottom
  nav** (`top` 319px) and the viewport (375px). In 320×568 portrait it is **31%**.
- **Fix:** collapse the filter bar into a single "Filters" affordance on mobile (search + a filter
  button that opens the panel as a sheet), instead of always-rendered search + 6 wrapping pills +
  toggle + count. Reclaims ~120px. Complexity M · Risk Low-Medium.
- This is the single biggest *rendered* mobile problem after P0-1 — the board is the product, and in
  landscape you see almost no cards.

### Not yet verified live (still open)

- **Home-indicator overlap** (needs physical notched iPhone).
- **Actual drag gesture feel** — options are proven; the on-finger scroll-vs-drag frustration should be
  felt on a real device to size the fix (delay value).
- **Workspaces board-tile reorder on touch** (HTML5 native DnD) — not exercised.
- **Landscape < 768 with many cards** — measured empty-ish; confirm with a full column.
- **Auth/register/invite/reset pages** — not driven (session was already authenticated).

### Revised readiness: still **6 / 10**

Structure is genuinely good (no overflow, contained scrolls, 52px nav targets, modals fit, focus rings,
debounced search). The score is held down by three *rendered* realities: (1) the board fights touch
(P0-1), (2) the board's filter chrome swallows 21–46% of the screen (LV-1), and (3) the calendar hides
half the week on a phone (P1-3). Fix those three and this is an 8.

---

## 8. Resolution (shipped 2026-07-09)

Every finding above was fixed and **verified live** with Chrome DevTools (emulated mobile+touch at
390×844, 320×568, 667×375 landscape). Measurements below are post-fix.

| ID | Fix | Verified |
|---|---|---|
| **P0-1** | `delay:180, delayOnTouchOnly:true, touchStartThreshold:8` on both SortableJS configs (`board-sortable.js`) — desktop mouse drag stays instant | options read back live |
| **P1-1** | `viewport-fit=cover` added to the viewport meta (`index.html`) | meta string confirmed |
| **P1-2** | Calendar surfaced as a mobile bottom-nav tab (`MainLayout.razor`) | 6 tabs present |
| **P1-3** | Agenda list replaces the 620px month grid below 600px (`Calendar.razor` + CSS) | grid `none`, agenda shown, 0 page overflow |
| **P2-1** | All text inputs → 16px on mobile, incl. the calendar select (specificity fix) | measured 16px |
| **P2-2** | Global `modal-a11y.js`: body scroll-lock, focus-in, Tab trap, Escape-close, focus restore — zero per-modal wiring, works on every `.modal.d-block` | focus in modal, `body{overflow:hidden}`, Escape closes |
| **P2-3** | Board chrome text `0.45/0.48 → 0.62` | — |
| **P2-4** | Column gear → 26×26 on coarse pointers | measured 26×26 |
| **P2-5** | Landscape column height bounded to clear the bottom nav (`168px` reserve) | 13px clearance, no overlap |
| **P2-6** | Touch move buttons on workspace board tiles (native DnD is mouse-only) | controls render 30×30, ends disable |
| **LV-1** | Quick-filter pills collapse behind the "Filters" toggle on mobile | header 179→145px (21%→17%) |
| **P3-1..P3-5** | Narrow mobile column (84vw), mobile back button, empty-column affordance, textarea hints, responsive due/priority row | — |

**Two follow-up fixes surfaced during live QA** (also shipped): even bottom-nav spacing (the
`.nav-*-wrap` wrappers weren't flex items, so Alerts/Account collapsed and touched), and
tap-outside-to-close for the Alerts/Account sheets (`.side-nav`'s `backdrop-filter` was clipping the
fixed backdrop to the 54px bar).

**Still needs a physical device** (emulator can't confirm): home-indicator overlap on notched iOS
(the `viewport-fit=cover` CSS is correct but the inset reads 0 in Chrome emulation), and the *feel*
of the 180 ms drag hold. The landscape column height uses a magic `168px` reserve — it clears the nav
today but a flex-based board layout would be the robust long-term fix.

New durable pattern: any future modal must render as `.modal.d-block` with a `.btn-close` to inherit
the `modal-a11y.js` behavior automatically (see `CLAUDE.md` → Frontend rules).

---

*Sections 1–6 were a static code audit; Section 7 is a live rendered pass that supersedes earlier
severities where noted; Section 8 records the shipped resolution. Remaining device-only items are
listed above.*
