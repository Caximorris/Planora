# Planora Design System — "Soft Glass Productivity UI"

Repo-specific design reference. The whole system is driven by CSS custom properties in
`Planora.Web/wwwroot/css/app.css` (`:root` + `html[data-theme="dark"]`). Component-scoped
tweaks live in `*.razor.css`. There is **no Tailwind / no CSS-in-JS** — Bootstrap 5 is present but
only for grid/utility classes and is overridden by our tokens.

## Direction

Restrained SaaS look (Linear / Notion / Superhuman lineage) on the **Iris** palette (violet primary,
cool slate neutrals, cyan accent). Glass is an **accent**, never the content. The app should read as
premium because it is consistent, readable and well-spaced — not because things are translucent.

## Glass rule (the important one)

Glass (`--color-surface-glass` / `--nav-glass` + `--glass-blur`) is used **only** on:
- desktop left rail + mobile bottom nav (`.side-nav`)
- command palette (`.search-container`)
- notification & account dropdowns (`.nav-notif-dropdown`, `.nav-dropdown`)
- modal overlay (`.modal-backdrop`, subtle 2px blur)

Everything else is a **solid, opaque** surface (`--color-surface`): kanban columns, cards, forms,
calendar cells, comments, lists, profile/settings panels. **Columns are explicitly not blurred** —
the previous `backdrop-filter` on `.kanban-column` was removed. The board *header* (`.board-header`)
keeps a light blur because it is a floating bar over the board cover, which is an approved use.

## Tokens (source of truth: `app.css`)

| Group | Tokens |
|-------|--------|
| Background | `--color-bg`, `--color-bg-subtle` |
| Surface | `--color-surface`, `--color-surface-2`, `--color-surface-elevated`, `--color-surface-glass`, `--nav-glass` |
| Border | `--color-border`, `--color-border-lo`, `--color-border-strong` |
| Text | `--color-text`, `--color-text-mid`, `--color-text-low` |
| Brand | `--color-primary` `#6d28d9`, `--color-primary-d/-hover`, `--color-accent` `#06b6d4`, `--color-accent-d/-l` |
| Status | `--color-danger` `#e11d48`, `--color-success` `#16a34a`, `--color-warning` `#d97706`, `--color-info` |
| Priority | explicit: Low=cyan, Medium=amber, High=`#be123c`, Critical=`#9f1239` |
| Radius | `--radius-xs 4 / sm 6 / md 10 / lg 14 / xl 20 / full` |
| Shadow | `--shadow-xs…xl` (cool indigo-slate), `--shadow-glass` |
| Motion | `--dur-1 120ms / --dur-2 180ms / --dur-3 240ms`, `--ease-out` (entrance spring), `--ease-in-out`; `--transition(-fast/-slow)` are full `duration easing` shorthands — never append another easing after them |
| Glass | `--glass-blur` = `blur(18px) saturate(140%)` |
| Layout | `--rail-w 76`, `--sidebar-w 256`, `--nav-h` (0 desktop / bottom-nav-h mobile), `--bottom-nav-h 60` |
| z-index | `--z-nav 200`, `--z-dropdown 500`, `--z-search 900`, `--z-modal 1050`, `--z-toast 1100` |

Legacy scale names (`--wine-*`=violet, `--rose-*`/`--lav-*`=slate, `--teal-*`=cyan, `--mint-*`=near-white)
are kept so existing references resolve; treat them as aliases, prefer the semantic tokens above for new code.

## Dark mode

First-class, token-based: `html[data-theme="dark"]` overrides only the semantic + glass tokens; every
component inherits. Persisted in `localStorage` (`planora-theme`), applied pre-Blazor by an inline
script in `index.html` (no flash). Canvas `#0f1020`, surface `#17182b`, glass ~0.72–0.80 opacity so it
stays legible. Never leave a light surface in dark mode; never drop below ~4.5:1 on body text.

## Components

- **Buttons** — Bootstrap `.btn-*` overridden in `app.css`: primary=violet, secondary/outline,
  danger=red, `.btn-sm`. Consistent radius/height, hover lift, `:focus-visible` ring (cyan).
- **Forms** — `.form-control/.form-select` on `--color-surface-2`, cyan focus ring, readable labels.
- **Kanban** — solid cards (`--kanban-card-bg`), priority left-border + badge, `@key` preserved on
  reordered lists, SortableJS drag states (`.sortable-ghost/-drag`). No transparency on cards.
- **Modals** — solid `.modal-content`, subtle blurred backdrop; close + Esc (search) preserved.
- **Nav** — glass rail (desktop) / glass bottom bar (mobile, `env(safe-area-inset-bottom)`), active
  route = accent pill + edge bar.

## Motion

Centralized "Motion system" section in `app.css` (before Reset & base). Everything is
transform/opacity; the single deliberate exception is the 5px checklist progress fill animating
`width` (that *is* the feature).

- **Durations**: `--dur-1` 120ms micro-interactions, `--dur-2` 180ms standard, `--dur-3` 240ms
  entrances. **Easings**: `--ease-out` (decel spring, `cubic-bezier(0.22,1,0.36,1)`) for entrances,
  `--ease-in-out` for state changes/exits.
- **Shared keyframes**: `fade-in`, `rise-in`, `drop-in`, `sheet-in` (mobile bottom sheets),
  `pop-in` (modals), `toast-out`, `skeleton-sweep`. Scoped `.razor.css` may reference them —
  Blazor CSS isolation rewrites selectors, not animation names (Landing does; SearchModal keeps
  private keyframes because its centering `translateX(-50%)` must live in every frame).
- **Where applied**: page roots rise in (`.board-root` **fades only** — a transform would break its
  `position:fixed` overlays); modals `pop-in` + backdrop fade; dropdowns `drop-in` (desktop rail
  fly-outs `rise-in`, mobile ≤768px sheets `sheet-in`); home grid + landing hero stagger via
  `backwards` fill + nth-child delays; `.skeleton` shimmer (compositor-only translated `::after`,
  variants `.nav-notif-skeleton`, `.board-tile-skeleton`); buttons/nav press `scale(0.98/0.95)`.
- **Toast exit**: two-phase dismiss in `ToastService` — `IsLeaving` flag → `toast-item--leaving`
  plays `toast-out` → removal after 200ms (`ExitAnimationDuration`, must stay ≥ `--dur-2`).
- **Exclusions (hard rule)**: no entrance animations on `@key`-ed sorted lists (kanban cards,
  board tiles) — Blazor re-inserts moved keyed nodes, replaying the animation on every drag.
  Modal *exits* are entrance-only by design (Blazor removes the node instantly).

## Accessibility

Global `:focus-visible` ring (cyan) on links/buttons/inputs/nav; icon-only nav buttons have
`aria-label` + `aria-expanded`; `@media (prefers-reduced-motion: reduce)` zeroes animation/transition
durations **and** `animation-delay` (staggered entrances use `backwards` fill and would otherwise be
held invisible). Modals get scroll-lock, focus-trap, Escape and focus-restore globally via
`modal-a11y.js` (any `.modal.d-block` + `.btn-close`). Known gap (deferred): kanban cards / board
tiles are clickable `<div>`s (no keyboard activation). See `docs/FRONTEND_AUDIT.md` A2.

## Responsive

Rail ≥769px; fixed bottom bar ≤768px with content bottom-padding; board scrolls horizontally;
`--nav-h` reserves mobile bottom space via every `calc(100vh - var(--nav-h))`. Small-mobile ≤480px
shrinks nav labels + modal margins.

## Known limitations / deferred

- Board-cover **default** color for brand-new boards is set in `.razor` (indigo now) — needs a
  server restart to reflect; existing boards keep saved covers.
- Notification bell dropdown duplicates `/notifications` page logic (audit U1).
- Some dead CSS remains post-nav-migration (`.nav-user-btn`, `.nav-chevron`) and dead template files
  (`Layout/NavMenu.*`, `MainLayout.razor.css`) — safe to remove in a dedicated cleanup.
