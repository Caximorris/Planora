# Planora — Session Reference

## What it is

Planora is a Kanban project management SaaS. Full-stack .NET 10 monorepo: REST API (ASP.NET Core), Blazor WASM frontend, shared DTO library. Deployed on Azure (Container Apps + Static Web Apps). The goal is a Trello/Linear-style tool with strong security defaults and a clean, opinionated UI.

---

## Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core Web API, .NET 10 |
| Frontend | Blazor WebAssembly, .NET 10 |
| Shared | Planora.Shared — DTOs, enums, constants |
| Database | PostgreSQL (port 5433 locally) via EF Core + Npgsql |
| Auth | ASP.NET Core Identity + JWT (15 min) + refresh tokens (7 days) |
| Mapping | Mapperly (source-generated, zero-reflection) |
| Validation | FluentValidation (Create* and Update* requests) |
| Drag & drop | SortableJS 1.15.6 (vendored) — columns & cards (touch-guarded: delay+threshold); HTML5 DnD — board tiles (mouse), touch move buttons on mobile |
| Storage | `IFileStorage` — `LocalFileStorage` (dev default); `BlobFileStorage` in production (private Azure Blob container, short-lived SAS read URLs applied to responses by `MediaUrlResolutionFilter`; see `docs/azure-blob-storage.md`) |
| Email | `IEmailSender` — console sink locally; Resend in production from `notifications@planora.website` |
| CI/CD | GitHub Actions → Docker → Azure Container Apps (API), Azure Static Web Apps (Web) |

---

## Project Structure

```
Planora.slnx
├── Planora.Api/
│   ├── Controllers/          One per entity, all [Authorize] except Auth + Demo
│   ├── Domain/Entities/      EF Core entities (User, Workspace, Board, Column, Card, …)
│   ├── Application/
│   │   ├── Mappers/          PlanoraMappingProfile — static partial, source-generated
│   │   ├── Services/         TokenService, RefreshTokenService, DemoWorkspaceSeeder
│   │   ├── Validators/       FluentValidation — Create* + Update*
│   │   └── Interfaces/
│   ├── Infrastructure/Data/  ApplicationDbContext, EF config
│   ├── Migrations/
│   └── wwwroot/uploads/      Board covers + card attachments (Local storage backend only — dev; prod uses Azure Blob)
├── Planora.Web/
│   ├── Auth/                 PlanorAuthStateProvider, AuthHeaderHandler
│   ├── Pages/                Landing, Login, Register, Home, Workspaces, Board, Profile, Notifications
│   ├── Components/           KanbanCard, KanbanColumn, SearchModal, shared UI
│   ├── Services/             AuthService, per-entity services, NotificationService, SearchService
│   ├── Layout/               MainLayout (nav, notifications bell, search button, user menu)
│   └── wwwroot/
│       ├── css/app.css
│       ├── js/               board-sortable.js, modal-a11y.js, theme.js, search.js, download.js
│       └── lib/sortablejs/
├── Planora.Shared/
│   ├── DTOs/                 Request/response types for every entity + Search
│   ├── Enums/                Priority, NotificationType, SearchResultType, …
│   └── Constants/            BoardLimits.MaxCoverImageBytes
└── Planora.Tests/           xUnit integration tests
    ├── Infrastructure/       PlanoraWebAppFactory (WebApplicationFactory<Program>)
    └── AuthFlowTests.cs      register/login/lockout/readiness flows
```

---

## Features (implemented)

### Core Kanban
- **Workspaces** — create/delete, drag-reorder board tiles (HTML5 DnD on desktop, ‹ › move buttons on touch)
- **Boards** — custom background color, cover image upload (5 MB, magic-bytes validated), archive/unarchive
- **Columns** — create, rename, color, reorder (SortableJS)
- **Cards** — title, description, priority (Low/Medium/High/Critical), due date, color, assignee, archive/unarchive
- **Card drag** — between columns and within same column (SortableJS + `@key` on Blazor foreach)
- **Mobile** — left rail → fixed bottom tab bar (6 tabs incl. Calendar); board filter collapses to a
  "Filters" toggle; Calendar swaps its month grid for an agenda list <600px; touch-guarded drag; 16px
  inputs (no iOS zoom); `viewport-fit=cover` + safe-area padding. Modals get scroll-lock / focus-trap /
  Escape globally via `modal-a11y.js`. See `docs/MOBILE_AUDIT.md`.
- **Priority filter** — pills at top of board filter visible cards by priority

### Collaboration
- **Workspace members** — invite via shareable token link (7-day expiry, email-matched), manage roles (Owner/Admin/Member)
- **Card assignee** — assign member from workspace roster
- **Card comments** — create and delete own comments with time-ago display
- **Labels** — create/assign custom labels per board
- **Checklists** — create checklist items per card
- **Transactional email** — email verification, password reset, workspace invites, card assignment emails, and assigned-card comment emails
- **Notification preferences** — profile toggles for assignment/comment/workspace-invite emails

### Discovery & UX
- Expired authenticated sessions show a branded, accessible wake-up state while the API cold-starts and rotates the refresh token; failed refreshes keep the existing safe redirect to sign-in.
- **Public landing page** — minimal hero with instant demo CTA
- **Instant demo** — `POST /api/auth/demo` creates guest account + seeds demo workspace; shown after 4 s as "Server is waking up…" (cold start on scale-to-zero)
- **Global search (Ctrl+K)** — full-text ILIKE search across cards and boards in all user workspaces; debounced 300 ms; keyboard navigation (↑↓ Enter Esc)
- **Notifications** — in-app bell, 30 s polling, unread badge, mark-all-read, dismiss
- **Dark mode** — `data-theme="dark"` on `<html>`, persisted in localStorage; kanban canvas excluded (pastel card colors need light text)
- **Profile page** — display name edit, email verification status/resend, theme switcher, password change, 2FA, sessions, notification preferences
- **Calendar view** — cards with due dates shown in a calendar on the Board page
- **Motion system** — centralized CSS animation layer in `app.css` (tokens `--dur-1/2/3`,
  `--ease-out`/`--ease-in-out`; shared keyframes `fade/rise/drop/sheet/pop-in`, `toast-out`,
  `skeleton-sweep`). Page-root/modal/dropdown entrances, home + landing staggers, skeleton loading
  states (notification dropdown, board tiles), two-phase animated toast dismiss. Transform/opacity
  only; `prefers-reduced-motion` zeroes durations **and** delays. No dependencies added.

### Security (hardened)
- 15-min JWT + 7-day rotating refresh tokens
- Refresh token reuse detection → all user tokens revoked
- Progressive lockout: 3 fails → 5 min, 5 → 15 min, 8 → 1 h, 10+ → 24 h
- Rate limiting: `fixed-window` 10 req/min on all auth endpoints
- SecurityStamp validated on every request (`OnTokenValidated`)
- IDOR prevention: every controller verifies `WorkspaceMembers` before returning data
- `UserId` always from JWT claims, never from request body
- Password reset and email verification use short-lived Identity tokens; reset rotates `SecurityStamp` and revokes refresh tokens
- HSTS, CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy
- Structured audit logging with correlation IDs
- **Data export & account deletion** — `GET /api/users/export` returns a full JSON snapshot of the
  user's profile + every workspace they belong to (archived included, trashed/other users' data
  excluded). `POST /api/users/delete-account` re-auths with the password, deletes solo-owned
  workspaces with the account, and blocks (409 + list) if the user still owns a workspace with other
  members. Logic in `AccountService` (`IAccountService`).

### Operational
- **Health checks** — `GET /health/live` (liveness, no checks) and `GET /health/ready`
  (readiness, DB `CanConnect` → 503 when DB down). For Azure Container Apps probes.
- **Integration tests** — `Planora.Tests` (xUnit) boots the API via `WebApplicationFactory<Program>`
  against a throwaway `planora_test` Postgres DB. Config injected via env vars (Program.cs reads
  `Jwt:Key` inline before host build). Run `dotnet test` with dev servers stopped.
- **Email provider switch** — `Email:Provider` selects `Console` or `Resend`; production requires
  `RESEND_API_KEY` in GitHub Actions and maps it to Azure Container Apps as
  `Email__Resend__ApiKey=secretref:resend-api-key`.

---

## Auth Flow

```
Register/Login → { token (15 min), refreshToken (7 days) } → localStorage
↓
Every request → AuthHeaderHandler attaches Bearer
↓
401 received → /api/auth/refresh (SemaphoreSlim prevents parallel refresh)
↓
Refresh rotates pair → store new tokens
↓
Logout → POST /api/auth/logout → SecurityStamp rotated → all tokens invalidated → clear localStorage
```

`PlanorAuthStateProvider.GetAuthenticationStateAsync` proactively refreshes before token expiry.

---

## API Controllers

| Controller | Key notes |
|---|---|
| AuthController | register, login + 2FA, refresh, logout, demo, password reset, email verification, sessions |
| WorkspacesController | member-gated; POST seeds demo board |
| BoardsController | cover image via separate endpoints; magic-byte validation |
| ColumnsController | position reorder |
| CardsController | priority, due date, color, assignee, archive |
| CommentsController | delete own only |
| LabelsController | board-scoped |
| ChecklistsController | card-scoped |
| InvitationsController | token-based, 7-day expiry |
| NotificationsController | unread count, mark read, dismiss |
| UsersController | profile update, password change, notification preferences, data export, account deletion |
| SearchController | GET /api/search?q= — ILIKE across boards + cards, min 2 chars |

---

## Key Patterns & Conventions

**Mapperly** — `PlanoraMappingProfile` is a `static partial class`. Matching property names map automatically — no manual mapper code needed.

**FluentValidation** — injected directly in constructor, called manually at the top of each action. Both `Create*Request` and `Update*Request` have validators. Update validators use partial-update semantics (`.When(x => x.Field is not null)`) so reorder/clear/assign-only updates pass; they enforce the same length/format/enum rules as create. Add the matching validator when introducing a new write flow — don't validate ad hoc in controllers.

**Blazor + SortableJS** — SortableJS reorders the DOM; Blazor's diffing re-renders using component state. Without `@key="card.Id"` on card foreach and `@key="col.Id"` on column foreach, Blazor assigns wrong data to wrong DOM nodes after a drag. Always add `@key`.

**Fixed modals** — any `position:fixed` overlay must be a sibling (not a child) of `.board-header` or `.kanban-column`. Both use `backdrop-filter` which creates a stacking context and breaks `inset:0`.

**SortableJS init** — `planoraInitColumnsSortable` and `planoraInitCardLists` are idempotent (check `dataset.sortableInit`). Call them every `OnAfterRenderAsync`.

**Uncontrolled inputs in Blazor** — search input has no `value="@_query"` binding. If bound, every debounce re-render overwrites the DOM value and loses intermediate keystrokes. Use `@oninput` + `_focusPending` flag for focus management.

**Motion system** — all animation goes through the tokens/keyframes in `app.css` ("Motion system" section, before Reset & base). Hard rules: **no entrance animations on `@key`-ed sorted lists** (kanban cards, board tiles) — Blazor moves keyed nodes by re-inserting them, replaying the animation on every drag/reorder; `.board-root` entrance is fade-only — a transform creates a containing block and breaks `position:fixed` descendants; desktop rail fly-outs use `rise-in` (bottom-anchored), mobile ≤768px dropdowns use `sheet-in`. Toast exit is two-phase in `ToastService`: `Dismiss` sets `IsLeaving` → CSS `toast-out` plays → removal after `ExitAnimationDuration` (200 ms — must stay ≥ `--dur-2`). Scoped `.razor.css` files can reference the shared keyframes (CSS isolation rewrites selectors, not animation names). Modal *exits* are infeasible (Blazor removes nodes instantly) — entrance-only by design.

**`dotnet watch` + hot reload** — CSS/JS changes hot-reload fine. C# Razor changes trigger ENC; if `dotnet watch` says "Waiting for file change" after a C# edit → kill the process and restart. Never run `dotnet build` while a dev server is live (fingerprint mismatch causes 404 on all Blazor assets).

**New `@page` route** — hot reload does not register new routes. Full server restart required.

---

## Deployment

Frontend deploys build a clean `artifacts/planora-web` publish directory, generate uncached
`version.json` metadata, validate its cache policy, and upload that exact directory to Azure Static
Web Apps. See [frontend-caching.md](docs/frontend-caching.md) for cache rules, service-worker
migration, and production header checks.

| Target | Platform | Trigger |
|---|---|---|
| Web | Azure Static Web Apps | Push to `main` |
| API | Azure Container Apps (Docker) | Push to `main` |
| DB | PostgreSQL (managed or self-hosted) | Manual |

- `API_BASE_URL` secret injected into `appsettings.json` at build time via `sed`
- API email delivery uses Resend in production: `Email__Provider=Resend`,
  `Email__From__Address=notifications@planora.website`, `App__WebBaseUrl=https://planora.website`,
  and `Email__Resend__ApiKey=secretref:resend-api-key` (sourced from GitHub `RESEND_API_KEY`).
- API port: `PORT` env var → fallback 8080
- Uploads: production runs `Storage__Provider=AzureBlob` (set in `deploy-api.yml`) with
  `Storage__Blob__ConnectionString=secretref:storage-connection-string` and
  `PublicBaseUrl=https://planorabs.blob.core.windows.net/uploads`. The container is **private**;
  reads are short-lived SAS URLs signed by `IFileStorage.GetReadUrl` and applied to every response
  by the global `MediaUrlResolutionFilter` — never return stored file URLs that bypass it. Dual-read
  keeps legacy `/uploads/...` covers resolving. Local dev defaults to `LocalFileStorage` on disk.
- Scale-to-zero on Container Apps → ~20 s cold start; demo page shows "Server is waking up…" after 4 s

---

## Known Issues / Technical Debt

- Card reorder is disabled while a priority filter is active (safe mitigation; the real fix —
  mapping the filtered index to the full-list position — is still unbuilt)
- API integration tests exist (`Planora.Tests`); no bUnit Blazor component tests yet
- `dotnet test` needs local Postgres up and dev servers stopped (API locks `Planora.Shared.dll`)
- `appsettings.Development.json` not committed — contains local DB password and dev JWT key

---

## Roadmap

- [ ] Real-time updates (SignalR)
- [ ] Board templates
- [ ] Analytics dashboard
- [x] Data export and account deletion
- [ ] Mobile layout improvements
- [x] Rate limiting on upload endpoints (per-user `uploads` policy, 15/min default)
- [x] `Update*Request` validators

---

## Local Dev

```
API:  http://localhost:8080   (PORT env var default)
Web:  http://localhost:5076
DB:   PostgreSQL port 5433
JWT:  dev-super-secret-key-minimum-32-characters-long!!
DB:   Host=localhost;Port=5433;Database=planora;Username=postgres;Password=admin1234
```

Migrations run automatically on API startup. Add with:
```bash
cd Planora.Api && dotnet ef migrations add <Name>
```

Run integration tests (needs local Postgres on 5433; stop dev servers first — the API locks
`Planora.Shared.dll`). Tests create and drop a separate `planora_test` database:
```bash
dotnet test Planora.slnx
```
