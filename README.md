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
| Storage | `IFileStorage` abstraction — local disk (dev), Azure Blob Storage in production with private container + short-lived SAS read URLs ([docs](docs/azure-blob-storage.md)) |
| Email | `IEmailSender` abstraction — console sink locally, Resend in production from `notifications@planora.website` |
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
- **Polished motion** — centralized CSS animation system (duration/easing tokens, shared keyframes), skeleton loading states, staggered entrances, animated toasts; `prefers-reduced-motion` respected; zero JS animation dependencies
- **Profile page** — display name, theme switcher, email status, notification preferences
- **Transactional email** — verification, password reset, workspace invites, card assignment/comment notifications
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
    "Default": "Host=localhost;Port=5433;Database=planora;Username=postgres;Password=yourpassword"
  },
  "Jwt": { "Key": "dev-super-secret-key-minimum-32-characters-long!!" },
  "App": { "WebBaseUrl": "http://localhost:5076" },
  "Cors": { "AllowedOrigins": "http://localhost:5076" },
  "Email": {
    "Provider": "Console",
    "From": {
      "Address": "notifications@planora.website",
      "Name": "Planora"
    },
    "Resend": {
      "ApiKey": ""
    }
  }
}
```

For a local real-send smoke test, switch `Email:Provider` to `Resend` in the gitignored
development settings and set `Email:Resend:ApiKey` to your Resend API key. Production uses the
GitHub `RESEND_API_KEY` secret; the deploy workflow stores it as an Azure Container Apps secret and
sets `Email__Resend__ApiKey=secretref:resend-api-key`.

2. Run both projects:

```bash
# Terminal 1 — API (port 8080)
cd Planora.Api && dotnet watch run

# Terminal 2 — Web (http://localhost:5076)
cd Planora.Web && dotnet watch run
```

Migrations run automatically on API startup. With the default local console email sink, register with
any invented email — a demo workspace is created immediately and verification/reset links are logged.

> **Note:** never run `dotnet build` while a dev server is live — it causes 404 fingerprint errors on Blazor WASM assets.

## Testing

API integration tests live in `Planora.Tests` (xUnit). They boot the real API in-memory with
`WebApplicationFactory<Program>` against a throwaway `planora_test` PostgreSQL database (created and
dropped per run — no Docker required).

```bash
# Needs local Postgres running on 5433; stop the dev servers first (the API locks Planora.Shared.dll)
dotnet test Planora.slnx
```

Repository Codex and Git quality automation is documented in
[docs/codex-hooks.md](docs/codex-hooks.md). It uses changed-file scope and three execution tiers so
small documentation or isolated project edits do not trigger the full solution pipeline.

## Health Checks

- `GET /health/live` — liveness (process up; runs no checks)
- `GET /health/ready` — readiness (PostgreSQL connectivity; returns 503 when the DB is unreachable)

## Deployment

- Push to `main` triggers CI for both targets automatically
- `API_BASE_URL` secret is injected into `appsettings.json` at build time
- API email delivery uses Resend. Required production settings are `Email__Provider=Resend`,
  `Email__From__Address=notifications@planora.website`, `App__WebBaseUrl=https://planora.website`,
  and the GitHub `RESEND_API_KEY` secret.
- File storage is selected by `Storage:Provider` (`Local` by default). Production deploys with
  `Storage__Provider=AzureBlob`: uploads go to a **private** Azure Blob container and are served as
  short-lived SAS URLs signed per response, so covers and attachments survive restarts and scale-out
  ([docs/azure-blob-storage.md](docs/azure-blob-storage.md))

## Roadmap

- [ ] Real-time updates (SignalR)
- [x] Email delivery for invitations and account recovery
- [ ] Board templates
- [ ] Analytics dashboard
- [x] Data export and account deletion
