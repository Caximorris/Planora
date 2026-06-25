# Planora

Kanban project management app built with .NET 10, Blazor WebAssembly, and PostgreSQL.

## Stack

| Layer | Technology |
|-------|-----------|
| Frontend | Blazor WASM (.NET 10) |
| Backend | ASP.NET Core Web API (.NET 10) |
| Database | PostgreSQL |
| Auth | ASP.NET Core Identity + JWT (15 min) + refresh tokens (7 days) |
| ORM | Entity Framework Core + Npgsql |
| Mapping | Mapperly (source-generated) |
| Validation | FluentValidation |
| Drag & Drop | SortableJS (columns/cards) + HTML5 DnD (board tiles) |

## Features

- **Public landing page** — onboarding intro, no account required to preview
- **Workspaces** — create and manage multiple workspaces, drag-and-drop board ordering
- **Boards** — custom background colors, cover image upload, drag-and-drop column ordering
- **Kanban columns** — create, rename, color, reorder via drag-and-drop (SortableJS)
- **Cards** — title, description, priority, due date, color, assignee, drag-and-drop between columns
- **Card comments** — create and delete your own comments with time-ago display
- **Workspace members** — invite via shareable link, manage roles (Owner / Admin / Member)
- **Token-based invitations** — 7-day expiry, email-matched accept/decline flow
- **Notifications** — in-app bell with unread count, 30-second polling, mark read/dismiss
- **Dark mode** — toggle in Profile → Appearance, persisted in localStorage
- **Profile page** — account info, theme switcher
- **Demo workspace** — created automatically on register so new users land on a working board

### Security

- 15-minute JWT access tokens + 7-day rotating refresh tokens
- Refresh token reuse detection (revoked token → all user tokens invalidated)
- Progressive account lockout (3 fails → 5 min … 10+ fails → 24 hours)
- Rate limiting on all auth endpoints (`fixed-window`, 10 req/min)
- SecurityStamp validation on every request (token invalidated on logout/password change)
- IDOR protection — all workspace-scoped endpoints verify membership
- HSTS, CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy
- Structured audit logging with correlation IDs

## Project Structure

```
Planora/
├── Planora.Api/          # ASP.NET Core Web API
│   ├── Controllers/
│   ├── Domain/Entities/
│   ├── Application/
│   │   ├── Mappers/      # Mapperly (static partial class)
│   │   ├── Services/     # TokenService, RefreshTokenService, DemoWorkspaceSeeder
│   │   └── Validators/   # FluentValidation (Create* requests only)
│   └── Infrastructure/Data/
├── Planora.Shared/       # DTOs and enums shared between API and Web
└── Planora.Web/          # Blazor WASM frontend
    ├── Auth/             # PlanorAuthStateProvider, AuthHeaderHandler
    ├── Components/       # KanbanCard, KanbanColumn
    ├── Layout/           # MainLayout
    ├── Pages/            # Landing, Login, Register, Home, Workspaces, Board, Profile
    ├── Services/         # AuthService, per-entity services, NotificationService
    └── wwwroot/
        ├── css/app.css
        ├── js/           # board-sortable.js, theme.js
        └── lib/sortablejs/
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL — this project uses port **5433** (not 5432)

### Database

Update the connection string in `Planora.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=planora;Username=postgres;Password=yourpassword"
  }
}
```

Migrations run automatically on API startup (`db.Database.Migrate()`). To run manually:

```bash
cd Planora.Api
dotnet ef database update
```

### Running

```bash
# Terminal 1 — API
cd Planora.Api && dotnet watch run

# Terminal 2 — Web (http://localhost:5076)
cd Planora.Web && dotnet watch run
```

> **Note:** never run `dotnet build` while a dev server is live — causes 404 fingerprint errors on the Blazor WASM assets.

### Trying it out

No real email needed — register with any invented address and you'll get a demo workspace with a sample board ready to use.

## Deployment

- **Web** → Azure Static Web Apps (CI on push to `main`)
- **API** → Azure Container Apps (CI on push to `main`, Docker image)
- CI injects `API_BASE_URL` secret into `appsettings.json` at build time
- Board cover images are volume-mounted (`board_covers`) so they survive container restarts

## Roadmap

- [ ] Email delivery for invitations
- [ ] Card labels and checklists
- [ ] Analytics dashboard
- [ ] Board templates
- [ ] Real-time updates (SignalR)
- [ ] Settings page (password change, 2FA, account deletion)
