# Planora

Kanban project management SaaS built with .NET 10, Blazor WebAssembly, and PostgreSQL.

## Stack

| Layer | Technology |
|-------|-----------|
| Frontend | Blazor WASM (.NET 10) |
| Backend | ASP.NET Core Web API (.NET 10) |
| Database | PostgreSQL |
| Auth | ASP.NET Core Identity + JWT |
| ORM | Entity Framework Core + Npgsql |
| Mapping | Mapperly (source-generated) |
| Validation | FluentValidation |
| Drag & Drop | SortableJS |

## Features

- **Workspaces** — create and manage multiple workspaces, drag-and-drop board ordering
- **Boards** — custom background colors, drag-and-drop column ordering
- **Kanban columns** — create, rename, reorder via drag-and-drop
- **Cards** — title, description, priority, due date, assignee, drag-and-drop between columns
- **Card comments** — create and delete your own comments with time-ago display
- **Workspace members** — invite via shareable link, manage roles (Owner / Admin / Member), remove members
- **Token-based invitations** — 7-day expiry, email-matched accept/decline flow
- **Authentication** — register, login, JWT stored in localStorage, profile page

## Project Structure

```
Planora/
├── Planora.Api/          # ASP.NET Core Web API
│   ├── Controllers/
│   ├── Domain/Entities/
│   └── Infrastructure/
│       ├── Data/         # EF Core DbContext, configurations, migrations
│       └── Validators/
├── Planora.Shared/       # DTOs and enums shared between API and Web
│   ├── DTOs/
│   └── Enums/
└── Planora.Web/          # Blazor WASM frontend
    ├── Auth/             # JWT auth state provider
    ├── Components/       # KanbanCard, KanbanColumn
    ├── Layout/           # MainLayout, NavMenu
    ├── Pages/
    ├── Services/         # HTTP service layer
    └── wwwroot/
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL (port 5433 by default for this project)

### Database

Create a database and update the connection string in `Planora.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=planora;Username=postgres;Password=yourpassword"
  }
}
```

Run migrations:

```bash
cd Planora.Api
dotnet ef database update
```

### Running

Open two terminals:

```bash
# Terminal 1 — API (http://localhost:5009)
cd Planora.Api
dotnet watch run

# Terminal 2 — Web (http://localhost:5076)
cd Planora.Web
dotnet watch run
```

Then open [http://localhost:5076](http://localhost:5076).

### Test account

```
Email:    test@planora.dev
Password: Test1234
```

## Roadmap

- [ ] Email delivery for invitations
- [ ] Notifications
- [ ] Card labels and checklists
- [ ] Analytics dashboard
- [ ] Board templates
- [ ] Real-time updates (SignalR)
