# Planora

Kanban project management app — .NET 10, Blazor WebAssembly, PostgreSQL, deployed on Azure.

## Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core Web API (.NET 10) |
| Frontend | Blazor WebAssembly (.NET 10) |
| Database | PostgreSQL via EF Core + Npgsql |
| Auth | ASP.NET Core Identity + JWT (15 min) + rotating refresh tokens (7 days) |
| Mapping | Mapperly (source-generated) |
| Validation | FluentValidation |
| Drag & Drop | SortableJS (columns/cards) + HTML5 DnD (board tiles) |
| Hosting | Azure Container Apps (API) + Azure Static Web Apps (frontend) |

## Features

- **Public landing page** with instant demo — no account needed to try
- **Workspaces** — multiple workspaces, drag-and-drop board ordering
- **Boards** — custom color, cover image upload, archive/unarchive
- **Kanban columns** — create, rename, color, drag-and-drop reorder
- **Cards** — title, description, priority, due date, color, assignee, labels, checklists, archive/unarchive
- **Card drag** — between columns and within the same column
- **Priority filter** — filter board cards by Low / Medium / High / Critical
- **Global search (Ctrl+K)** — search cards and boards across all workspaces
- **Workspace members** — invite via shareable link, roles (Owner / Admin / Member)
- **Card comments** with time-ago display
- **Notifications** — in-app bell, unread badge, 30 s polling, mark-read, dismiss
- **Calendar view** — cards with due dates shown on a calendar
- **Dark mode** — persisted in localStorage
- **Profile page** — display name, theme switcher
- **Demo workspace** — auto-created on first login with sample board

### Security

- Short-lived JWT (15 min) + rotating refresh tokens with reuse detection
- Progressive account lockout (3 fails → 5 min … 10+ → 24 h)
- Rate limiting on all auth endpoints
- SecurityStamp invalidation on logout / password change
- IDOR protection on every workspace-scoped endpoint
- HSTS, CSP, X-Frame-Options, Referrer-Policy, X-Content-Type-Options

## Getting Started

**Prerequisites:** .NET 10 SDK, PostgreSQL

1. Create the database and update `Planora.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=planora;Username=postgres;Password=yourpassword"
  },
  "Jwt": { "Key": "dev-super-secret-key-minimum-32-characters-long!!" },
  "ApiBaseUrl": "http://localhost:8080"
}
```

2. Run both projects:

```bash
# Terminal 1 — API (port 8080)
cd Planora.Api && dotnet watch run

# Terminal 2 — Web (http://localhost:5076)
cd Planora.Web && dotnet watch run
```

Migrations run automatically on API startup. Register with any invented email — a demo workspace is created immediately.

> **Note:** never run `dotnet build` while a dev server is live — it causes 404 fingerprint errors on Blazor WASM assets.

## Testing

API integration tests live in `Planora.Tests` (xUnit). They boot the real API in-memory with
`WebApplicationFactory<Program>` against a throwaway `planora_test` PostgreSQL database (created and
dropped per run — no Docker required).

```bash
# Needs local Postgres running on 5433; stop the dev servers first (the API locks Planora.Shared.dll)
dotnet test Planora.slnx
```

## Health Checks

- `GET /health/live` — liveness (process up; runs no checks)
- `GET /health/ready` — readiness (PostgreSQL connectivity; returns 503 when the DB is unreachable)

## Deployment

- Push to `main` triggers CI for both targets automatically
- `API_BASE_URL` secret is injected into `appsettings.json` at build time
- Board cover images are volume-mounted and survive container restarts

## Roadmap

- [ ] Real-time updates (SignalR)
- [ ] Email delivery for invitations
- [ ] Board templates
- [ ] Analytics dashboard
- [ ] Settings page (password change, 2FA, account deletion)
