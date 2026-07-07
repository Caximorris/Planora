# Planora Frontend Audit — 2026-07-07

Scope: `Planora.Web` (Blazor WebAssembly, .NET 10). Traced every page, layout, component,
service, and JS interop file. This document records findings and the changes made in this pass
(a navigation redesign + the safe, high-value fixes surfaced during the audit).

---

## Executive summary

The frontend is in good structural shape for a portfolio app: a clean one-service-per-domain
layer (`Services/*Service`), no `HttpClient` calls leaking into components, sound optimistic
drag/drop with debounced persistence, and a real token-refresh pipeline. The biggest problems were
**not** architectural — they were a large set of **undefined CSS variables** that left several
surfaces (Calendar grid, Notifications page, checklists, label manager, search hover) silently
unstyled, and one **latent auth-decoding bug**. Both are fixed here.

The requested navigation change (top bar → left icon rail on desktop/tablet, fixed bottom tab bar
on mobile) was implemented with a contained diff: markup in `MainLayout.razor` and CSS in
`app.css`. All interactive nav functionality (search, notifications dropdown, account dropdown,
sign-out) is preserved on every viewport.

---

## Current architecture map

```
App.razor ── Router + CascadingAuthenticationState + AuthorizeRouteView(DefaultLayout=MainLayout)
  └── MainLayout.razor          app-shell: <side-nav> (auth only) + <main> ErrorBoundary @Body
        ├── SearchModal.razor    Ctrl+K modal (uncontrolled input, 300ms debounce)
        └── Pages/
             ├── Landing / Login / Register / InviteAccept   (unauth — no nav rendered)
             ├── Home            dashboard cards → workspaces/calendar/notifications
             ├── Workspaces      ws-sidebar + board tiles (HTML5 DnD reorder) + modals
             ├── Board           columns/cards (SortableJS) + card modal + settings modals
             ├── Calendar        month grid, per-workspace due dates
             ├── Notifications   full list (mark-read / dismiss)
             └── Profile         name / theme / password / (soon: 2FA, sessions…)
Auth/  PlanorAuthStateProvider (JWT parse + proactive refresh) · AuthHeaderHandler (401 → refresh)
Services/  Auth, Workspace, Board, Column, Card, Comment, Label, Checklist, Notification, User, Search
JS/  board-sortable.js (SortableJS interop) · theme.js · search.js
State: component-local; NotificationService is the one shared stateful service (unread count + 30s poll)
```

Theme: `data-theme="dark"` on `<html>`, persisted in localStorage, applied pre-Blazor by an inline
script in `index.html` (no FOUC). Every color is a CSS custom property, so the toggle is one
attribute flip — no re-render.

---

## Main risks

1. **CSS token drift** — two naming conventions coexisted (`--color-*`/`--rose-*` vs a legacy
   `--bg-*`/`--text-*`/`--cream-*`/`--olive-*` set). The legacy names were referenced in 85 places
   but **never defined**, so those declarations were dropped by the browser. (FIXED — aliased.)
2. **Layout math coupling** — a dozen rules use `calc(100vh - var(--nav-h))`. The nav redesign had
   to preserve that contract; it does, by repurposing `--nav-h` as "reserved chrome height"
   (0 on desktop rail, bottom-bar height on mobile).
3. **Backdrop-filter stacking** — `.board-header` / `.kanban-column` create stacking contexts;
   modals are already rendered as siblings of the board root, so this stayed correct.

---

## Bugs found

| # | Sev | Finding | Status |
|---|-----|---------|--------|
| B1 | P1 | **85 undefined CSS variables.** `--border`, `--bg-primary/secondary`, `--text-primary/secondary`, `--bg-hover`, `--color-hover`, bare `--radius`, `--cream-100..600`, `--olive-700` are referenced but not in `:root`. Effect: Calendar grid renders with **no borders and no cell backgrounds**; Notifications page items, checklists and label-manager have **transparent/wrong backgrounds**; search result hover/keyboard-cursor highlight is **invisible** (`--color-hover`); notif dropdown loses its border-radius (`--radius`). | **FIXED** — added a compatibility alias block in `:root` mapping each legacy name to a real semantic token (adapts to dark mode automatically). |
| B2 | P1 | **JWT base64url decode bug.** `PlanorAuthStateProvider.ParseClaims` calls `Convert.FromBase64String` on the raw payload without converting base64url (`-`→`+`, `_`→`/`). Any token whose payload contains those chars throws `FormatException` in `GetAuthenticationStateAsync` (uncaught) → broken auth state. | **FIXED** — added `.Replace('-','+').Replace('_','/')` before decode. |
| B3 | P2 | **Sign-out unreachable from Profile page.** Logout lived only in the top-nav dropdown. On a mobile bottom bar a plain "Profile" link would strand the user with no way to sign out. | **MITIGATED** — the account dropdown (with Sign out) is preserved as the mobile bottom bar's Account item, opening upward above the bar. No functionality lost. |
| B4 | P3 | `FocusOnNavigate Selector="h1"` in `App.razor`, but Board, Calendar, Notifications and Workspaces have no `<h1>` — focus target silently missing on those routes. | Deferred (cosmetic a11y). |

---

## UX issues

- **U1 (P2)** — Notification logic is **duplicated**: `TimeAgo`, `NotifIcon`, `OpenNotification`,
  `Dismiss`/`DeleteAsync`, `MarkAllRead` exist in both `MainLayout.razor` (bell dropdown) and
  `Notifications.razor` (page). Candidate for extraction into `NotificationService` /a shared
  component. Deferred — behavior-preserving refactor, out of scope for this pass.
- **U2 (P2)** — Calendar and Workspaces silently render empty on a failed API call (no error
  state). Only a spinner → empty. Deferred.
- **U3 (P3)** — Card modal color picker defaults to `#f8f5ed` (legacy cream) while board defaults
  are olive-green (`#253018`, `#3d4d27`, `#4a5d30`) — neither matches the wine/teal brand palette.
  Cosmetic legacy drift. Deferred.

## Responsive issues

- **R1 (P2, documented limitation)** — Board **tile** reorder in `Workspaces.razor` uses native
  HTML5 `draggable` DnD, which has **no touch support** → boards can't be reordered on mobile.
  Card/column DnD uses SortableJS with `forceFallback:true`, which *does* work on touch.
- **R2 (fixed by this pass)** — Previously the only responsive nav handling shrank the top bar and
  hid links below 480px. Now replaced by a proper bottom tab bar with `env(safe-area-inset-bottom)`
  and 52px+ touch targets.

## Accessibility issues

- **A1 (fixed)** — Icon-only nav controls now carry `aria-label` (Search, Notifications, Account)
  and `aria-expanded` on the dropdown triggers; the rail is `<nav aria-label>`.
- **A2 (P2, deferred)** — Kanban cards, notification rows and board tiles are clickable `<div>`s
  (no keyboard focus/activation). Should be `<button>` or get `role/tabindex/keydown`.
- **A3 (P2, deferred)** — Bootstrap-style modals (Board card/settings, Workspaces create/members)
  lack `role="dialog"`/`aria-modal`, focus trapping, and Escape-to-close (only `SearchModal` traps
  Esc). Card modal closes only via backdrop/Cancel.

## Performance issues

- **P1p (P3)** — `Board.OnAfterRenderAsync` calls two JS interop functions on **every** render.
  They're idempotent (guarded by `dataset.sortableInit`), so it's cheap but chatty. Could gate on
  "columns/cards changed". Deferred.
- **P2p (P3)** — `NotificationService` polls every 30s regardless of tab visibility. Could pause on
  `document.hidden`. Deferred.

## Code-structure issues

- **S1 (P2)** — Dead template leftovers: `Layout/NavMenu.razor` (empty), `Layout/NavMenu.razor.css`
  (unused Bootstrap boilerplate), `Layout/MainLayout.razor.css` (`.page`/`.sidebar`/`.top-row` — none
  used by current markup). Harmless but confusing. Left in place this pass (deleting scoped CSS is a
  separate, reviewable change).
- **S2 (P3)** — Now-unused CSS rules after the nav change: `.nav-user-btn`, `.nav-chevron(.--up)`,
  `.nav-notif-btn`. Left for a dedicated CSS cleanup pass to keep this diff focused on behavior.
- **S3 (good)** — Services are clean and single-domain; no `HttpClient` in components; Mapperly/DTO
  discipline holds on the API side. No action.

## Security / auth (frontend)

- **SEC1 (accepted)** — Access + refresh tokens live in `localStorage` (standard for Blazor WASM;
  XSS-exposed by design). Refresh-token **reuse detection** is server-side. Acceptable for this app;
  documented, not changed.
- **SEC2 (fixed)** — See B2 (base64url). No token/secret is logged anywhere in `Planora.Web`.
- **SEC3 (good)** — `AuthHeaderHandler` refreshes on 401 for non-auth endpoints only, uses a bare
  client to avoid recursion, and a `SemaphoreSlim` to avoid refresh storms. No change.

---

## Recommended fixes, ranked

**P0** — none outstanding.

**P1**
- ✅ B1 undefined CSS variables (fixed)
- ✅ B2 JWT base64url decode (fixed)

**P2**
- U1 consolidate notification dropdown/page logic
- A2 keyboardable cards/tiles; A3 modal focus-trap + Esc
- S1 remove dead template files (`NavMenu.*`, `MainLayout.razor.css`)
- R1 optional: touch DnD for board tiles (or a mobile move-affordance)

**P3**
- U2 error states (Calendar/Workspaces), U3 palette drift on color defaults
- P1p/P2p render/poll efficiency, B4 `h1` focus targets, S2 dead CSS sweep

---

## Changes implemented in this pass

**Navigation redesign**
- `Layout/MainLayout.razor` — top `<header class="top-nav">` replaced by a vertical
  `<nav class="side-nav">` icon rail wrapped in an `.app-shell` flex container. Items: brand→Home,
  Home, Spaces, Calendar (rail-only), Search, Alerts (notif dropdown), spacer, Account (dropdown).
  Notification + account dropdown markup unchanged; only the triggers restyled. `@code` untouched.
- `wwwroot/css/app.css`
  - Replaced the `.top-nav*` block with `.app-shell` / `.side-nav` / `.side-nav-item` styles.
  - `--nav-h` repurposed to "reserved chrome height" (`0` desktop, `--bottom-nav-h` mobile) so all
    existing `calc(100vh - var(--nav-h))` rules keep working; added `--rail-w`, `--bottom-nav-h`.
  - Desktop/tablet (≥769px): sticky full-height left rail; dropdowns fly out to the right.
  - Mobile (≤768px): rail becomes a `position:fixed` bottom tab bar (Home, Spaces, Search, Alerts,
    Account — Calendar hidden via `--rail-only`), `env(safe-area-inset-bottom)` padding, dropdowns
    open upward, scrollable page roots get bottom padding so content is never covered.
  - Removed dead `.top-nav-*` rules from the `≤480px` block.

**Bug fixes**
- `wwwroot/css/app.css` — added the `:root` compatibility-alias block (B1).
- `Auth/PlanorAuthStateProvider.cs` — base64url normalization in `ParseClaims` (B2).

**Accessibility**
- `aria-label` / `aria-expanded` on icon-only nav controls; `<nav aria-label>` landmark (A1).

## Changes intentionally deferred

U1 (notif de-dup), U2/U3, A2/A3 (keyboardable cards + modal focus-trap), R1 (touch tile DnD),
S1/S2 (dead file/CSS removal), P1p/P2p, B4. Each is a separate reviewable change and none blocks
the navigation work. Rationale: keep this diff small, behavior-preserving, and easy to review.

---

## Manual browser test checklist

Nav-critical (new): desktop rail renders + active highlight; tablet still shows rail; mobile shows
bottom bar with safe-area gap; Search/Alerts/Account dropdowns open (right on desktop, upward on
mobile) and close via backdrop; content never hidden behind the bar; dark mode on rail + bar.
Regression: login/logout, expired-token refresh, Ctrl+K search, workspace switch, board open,
board/column/card ordering + cross-column moves, card modal, comments, notifications badge/mark-read,
calendar month nav, profile save/theme, archive/unarchive, invite link flow.
