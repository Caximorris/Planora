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
| Validation | FluentValidation (Create* requests only) |
| Drag & drop | SortableJS 1.15.6 (vendored) — columns & cards; HTML5 DnD — board tiles |
| Storage | Local volume / Azure Files — board cover images |
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
│   │   ├── Validators/       FluentValidation — Create* only
│   │   └── Interfaces/
│   ├── Infrastructure/Data/  ApplicationDbContext, EF config
│   ├── Migrations/
│   └── wwwroot/uploads/      Board cover images (volume-mounted in prod)
├── Planora.Web/
│   ├── Auth/                 PlanorAuthStateProvider, AuthHeaderHandler
│   ├── Pages/                Landing, Login, Register, Home, Workspaces, Board, Profile, Notifications
│   ├── Components/           KanbanCard, KanbanColumn, SearchModal, shared UI
│   ├── Services/             AuthService, per-entity services, NotificationService, SearchService
│   ├── Layout/               MainLayout (nav, notifications bell, search button, user menu)
│   └── wwwroot/
│       ├── css/app.css
│       ├── js/               board-sortable.js, theme.js, search.js
│       └── lib/sortablejs/
└── Planora.Shared/
    ├── DTOs/                 Request/response types for every entity + Search
    ├── Enums/                Priority, NotificationType, SearchResultType, …
    └── Constants/            BoardLimits.MaxCoverImageBytes
```

---

## Features (implemented)

### Core Kanban
- **Workspaces** — create/delete, drag-reorder board tiles (HTML5 DnD)
- **Boards** — custom background color, cover image upload (5 MB, magic-bytes validated), archive/unarchive
- **Columns** — create, rename, color, reorder (SortableJS)
- **Cards** — title, description, priority (Low/Medium/High/Critical), due date, color, assignee, archive/unarchive
- **Card drag** — between columns and within same column (SortableJS + `@key` on Blazor foreach)
- **Priority filter** — pills at top of board filter visible cards by priority

### Collaboration
- **Workspace members** — invite via shareable token link (7-day expiry, email-matched), manage roles (Owner/Admin/Member)
- **Card assignee** — assign member from workspace roster
- **Card comments** — create and delete own comments with time-ago display
- **Labels** — create/assign custom labels per board
- **Checklists** — create checklist items per card

### Discovery & UX
- **Public landing page** — minimal hero with instant demo CTA
- **Instant demo** — `POST /api/auth/demo` creates guest account + seeds demo workspace; shown after 4 s as "Server is waking up…" (cold start on scale-to-zero)
- **Global search (Ctrl+K)** — full-text ILIKE search across cards and boards in all user workspaces; debounced 300 ms; keyboard navigation (↑↓ Enter Esc)
- **Notifications** — in-app bell, 30 s polling, unread badge, mark-all-read, dismiss
- **Dark mode** — `data-theme="dark"` on `<html>`, persisted in localStorage; kanban canvas excluded (pastel card colors need light text)
- **Profile page** — display name edit, theme switcher
- **Calendar view** — cards with due dates shown in a calendar on the Board page

### Security (hardened)
- 15-min JWT + 7-day rotating refresh tokens
- Refresh token reuse detection → all user tokens revoked
- Progressive lockout: 3 fails → 5 min, 5 → 15 min, 8 → 1 h, 10+ → 24 h
- Rate limiting: `fixed-window` 10 req/min on all auth endpoints
- SecurityStamp validated on every request (`OnTokenValidated`)
- IDOR prevention: every controller verifies `WorkspaceMembers` before returning data
- `UserId` always from JWT claims, never from request body
- HSTS, CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy
- Structured audit logging with correlation IDs

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
| AuthController | register, login (progressive lockout), refresh, logout, demo |
| WorkspacesController | member-gated; POST seeds demo board |
| BoardsController | cover image via separate endpoints; magic-byte validation |
| ColumnsController | position reorder |
| CardsController | priority, due date, color, assignee, archive |
| CommentsController | delete own only |
| LabelsController | board-scoped |
| ChecklistsController | card-scoped |
| InvitationsController | token-based, 7-day expiry |
| NotificationsController | unread count, mark read, dismiss |
| UsersController | profile update |
| SearchController | GET /api/search?q= — ILIKE across boards + cards, min 2 chars |

---

## Key Patterns & Conventions

**Mapperly** — `PlanoraMappingProfile` is a `static partial class`. Matching property names map automatically — no manual mapper code needed.

**FluentValidation** — injected directly in constructor, called manually. Only `Create*Request` has validators. `Update*Request` are unvalidated (add if modifying those flows).

**Blazor + SortableJS** — SortableJS reorders the DOM; Blazor's diffing re-renders using component state. Without `@key="card.Id"` on card foreach and `@key="col.Id"` on column foreach, Blazor assigns wrong data to wrong DOM nodes after a drag. Always add `@key`.

**Fixed modals** — any `position:fixed` overlay must be a sibling (not a child) of `.board-header` or `.kanban-column`. Both use `backdrop-filter` which creates a stacking context and breaks `inset:0`.

**SortableJS init** — `planoraInitColumnsSortable` and `planoraInitCardLists` are idempotent (check `dataset.sortableInit`). Call them every `OnAfterRenderAsync`.

**Uncontrolled inputs in Blazor** — search input has no `value="@_query"` binding. If bound, every debounce re-render overwrites the DOM value and loses intermediate keystrokes. Use `@oninput` + `_focusPending` flag for focus management.

**`dotnet watch` + hot reload** — CSS/JS changes hot-reload fine. C# Razor changes trigger ENC; if `dotnet watch` says "Waiting for file change" after a C# edit → kill the process and restart. Never run `dotnet build` while a dev server is live (fingerprint mismatch causes 404 on all Blazor assets).

**New `@page` route** — hot reload does not register new routes. Full server restart required.

---

## Deployment

| Target | Platform | Trigger |
|---|---|---|
| Web | Azure Static Web Apps | Push to `main` |
| API | Azure Container Apps (Docker) | Push to `main` |
| DB | PostgreSQL (managed or self-hosted) | Manual |

- `API_BASE_URL` secret injected into `appsettings.json` at build time via `sed`
- API port: `PORT` env var → fallback 8080
- Board images: volume-mounted (`board_covers`) — survive container restarts
- Scale-to-zero on Container Apps → ~20 s cold start; demo page shows "Server is waking up…" after 4 s

---

## Known Issues / Technical Debt

- `Update*Request` validators not implemented (only `Create*`)
- No rate limiting on cover image upload endpoint
- SortableJS `evt.newIndex` is relative to filtered list when priority filter is active — reorder sends wrong position
- No test projects (deliberate; if adding: WebApplicationFactory for API, bUnit for Blazor)
- `appsettings.Development.json` not committed — contains local DB password and dev JWT key

---

## Roadmap

- [ ] Real-time updates (SignalR)
- [ ] Email delivery for invitations
- [ ] Board templates
- [ ] Analytics dashboard
- [ ] Settings page (password change, 2FA, account deletion)
- [ ] Mobile layout improvements
- [ ] Rate limiting on upload endpoints
- [ ] `Update*Request` validators

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
