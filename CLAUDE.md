# Planora — CLAUDE.md

## Commands

```bash
# API (port 5009 dev, 8080 prod/docker)
cd Planora.Api && dotnet watch run

# Web (port 5076 dev)
cd Planora.Web && dotnet watch run

# Never run `dotnet build` while a dev server is live (404 fingerprint errors).
# New .razor @page: must fully restart — hot-reload won't register the route.

# Migrations
cd Planora.Api
dotnet ef migrations add <MigrationName>
dotnet ef database update   # or just start the API — db.Database.Migrate() runs on boot

# Build solution
dotnet build Planora.slnx
```

**Local DB:** PostgreSQL port **5433** (not 5432 — that's a different instance for another project).  
Connection: `Host=localhost;Port=5433;Database=planora;Username=postgres;Password=admin1234`  
Dev JWT key: `dev-super-secret-key-minimum-32-characters-long!!`

## Stack & Structure

.NET 10, ASP.NET Core API + Blazor WASM, PostgreSQL via EF Core + Npgsql, ASP.NET Identity, Mapperly, FluentValidation, SortableJS (vendored), Blazored.LocalStorage.

```
Planora.slnx
├── Planora.Api/           ASP.NET Core REST API (Docker → Azure Container Apps)
│   ├── Controllers/       One controller per entity, all [Authorize] except AuthController
│   ├── Domain/Entities/   EF Core entities
│   ├── Application/
│   │   ├── Mappers/       Mapperly (static partial class — source-generated)
│   │   ├── Services/      TokenService, RefreshTokenService, DemoWorkspaceSeeder
│   │   ├── Validators/    FluentValidation — only Create* requests, not Update*
│   │   └── Interfaces/
│   ├── Infrastructure/Data/  ApplicationDbContext + EF config
│   ├── Migrations/        EF Core migration files
│   └── wwwroot/uploads/   Board cover images (volume-mounted in prod)
├── Planora.Web/           Blazor WASM (Azure Static Web Apps)
│   ├── Auth/              PlanorAuthStateProvider, AuthHeaderHandler
│   ├── Pages/             Home, Workspaces, Board, Profile, Login, Register
│   ├── Services/          AuthService + per-entity services
│   ├── Components/        Shared Blazor components
│   └── wwwroot/
│       ├── js/            board-sortable.js, theme.js (interop bridges)
│       ├── lib/sortablejs/ Vendored SortableJS 1.15.6
│       └── staticwebapp.config.json  CSP + MIME types for Azure SWA
└── Planora.Shared/        DTOs, Enums, Constants shared between Api and Web
    └── Constants/BoardLimits.cs  MaxCoverImageBytes — single source of truth
```

## Conventions

**Auth/authorization:**
- `UserId` always from `User.FindFirstValue(ClaimTypes.NameIdentifier)` — never trust request body for identity.
- All workspace-scoped resources: verify `WorkspaceMembers` before returning data (IDOR prevention).
- New sensitive auth endpoints get `[EnableRateLimiting("auth")]`.
- Progressive lockout is manual in `AuthController.Login` — Identity's `MaxFailedAccessAttempts` is set to 100 to prevent it from auto-resetting the counter.

**Mapperly:**
- `PlanoraMappingProfile` is `static partial class` with extension methods (`entity.ToDto()`).
- Adding a property with the same name to both entity and DTO is enough — no changes needed to the mapper.

**FluentValidation:**
- Injected as `IValidator<T>` directly in the controller constructor (not `AddFluentValidationAutoValidation`).
- Call `_validator.ValidateAsync(request)` manually and return `BadRequest(validation.Errors.Select(e => e.ErrorMessage))`.
- Only `Create*Request` validators exist. `Update*Request` types are unvalidated — add validators if modifying update flows.

**Board cover images:**
- Upload/delete via dedicated endpoints. `PUT /api/boards/{id}` does NOT accept `CoverImageUrl`.
- Magic bytes are validated server-side, not just Content-Type.
- `BoardLimits.MaxCoverImageBytes` is used by both API (`[RequestSizeLimit]`) and Web (`IBrowserFile.OpenReadStream(maxAllowedSize)`).

**Blazor UI rules:**
- Any UI that needs `position:fixed` to cover the viewport MUST NOT be nested inside `.board-header` or `.kanban-column` — both have `backdrop-filter` which creates a new containing block and breaks `inset:0`. Use the Bootstrap modal pattern (sibling, not child).
- SortableJS handles column/card DnD (`board-sortable.js`). `planoraInitColumnsSortable` and `planoraInitCardLists` are idempotent (check `dataset.sortableInit`) — call them every `OnAfterRenderAsync`.
- Board tile drag-reorder in `Workspaces.razor` still uses HTML5 native DnD — intentionally not migrated.
- Dark mode: `data-theme="dark"` on `<html>`. The kanban canvas (cards/columns background) is intentionally excluded from dark mode — card/column colors are user-chosen pastels that require dark text.

**Deployment:** CI injects `API_BASE_URL` secret into `appsettings.json` via `sed` (the repo file has the localhost dev URL). API port in prod comes from `PORT` env var, fallback 8080.

## Agent Rules

- **Before modifying any DTO in `Planora.Shared/`**: check both `Planora.Api` and `Planora.Web` — it's a shared contract. Confirm both sides still compile before committing.
- **Before adding a migration**: verify the API builds cleanly first (`dotnet build Planora.Api`).
- **Pushing to `main` triggers auto-deploy** to Azure Static Web Apps (Web) and Azure Container Apps (API). Ask before pushing — never push speculatively.
- **No test projects exist** — don't reference xUnit/NUnit or suggest running tests. If adding tests becomes the task, treat it as its own feature (choose framework, add project to slnx, use WebApplicationFactory for controllers, bUnit for Blazor).
- **`appsettings.Development.json` contains real local credentials** — never commit changes to that file.
- The `wwwroot/uploads/boards/` folder is in `.gitignore` (it holds user-uploaded images). Don't add it to git.

## References

- [docs/architecture.md](docs/architecture.md) — Auth flow, authorization model, deployment, known gaps
- [docs/api-endpoints.md](docs/api-endpoints.md) — Full endpoint reference with auth/rate-limit notes
