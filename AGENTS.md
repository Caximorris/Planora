# Planora — AGENTS.md

## Commands

```bash
cd Planora.Api  && dotnet watch run   # API → port 8080 (PORT env var, not launchSettings)
cd Planora.Web  && dotnet watch run   # Web → http://localhost:5076
dotnet build Planora.slnx             # build check (never while a dev server is live)
dotnet test  Planora.slnx             # integration tests — needs local Postgres; stop dev servers first
cd Planora.Api && dotnet ef migrations add <Name>   # migrations run on API boot automatically
```

**Local:** PostgreSQL port **5433**, DB `planora`, user `postgres`, pass `admin1234`.  
Dev JWT key: `dev-super-secret-key-minimum-32-characters-long!!`  
Full stack, patterns and roadmap: [session.md](session.md)

## Stack

.NET 10 · ASP.NET Core API · Blazor WASM · PostgreSQL · EF Core · ASP.NET Identity · Resend email · Mapperly · FluentValidation · SortableJS (vendored) · Blazored.LocalStorage

```
Planora.slnx
├── Planora.Api/     Controllers/ Domain/Entities/ Application/{Interfaces,Options,Mappers,Services,Validators} Infrastructure/{Data,Email,Jobs,Storage}/ Migrations/
├── Planora.Web/     Auth/ Pages/ Components/ Services/ Layout/ wwwroot/{css,js,lib/sortablejs}
├── Planora.Shared/  DTOs/ Enums/ Constants/BoardLimits.cs
└── Planora.Tests/   xUnit — Infrastructure/PlanoraWebAppFactory, AuthFlowTests
```

## Critical Rules

**Hot reload**
- Never run `dotnet build` while a dev server is live → fingerprint mismatch → 404 on all Blazor WASM assets.
- C# Razor changes that fail ENC → kill `dotnet watch` and restart; do not `--no-verify` workarounds.
- New `@page` route → full restart required (hot reload won't register it).

**Shared DTOs (`Planora.Shared/`)**
- Any DTO change is a shared contract — verify both `Planora.Api` and `Planora.Web` still compile before committing.

**Migrations**
- Run `dotnet build Planora.Api` cleanly before `dotnet ef migrations add`.

**Deployment**
- Push to `main` auto-deploys API (Azure Container Apps) and Web (Azure Static Web Apps). Ask before pushing.
- `appsettings.Development.json` has real local credentials — never commit it.
- Resend production email uses GitHub secret `RESEND_API_KEY`; the deploy workflow maps it to
  Azure Container Apps as `Email__Resend__ApiKey=secretref:resend-api-key`.

**Tests**
- `Planora.Tests` (xUnit) covers the API via `WebApplicationFactory<Program>` against a throwaway
  `planora_test` Postgres DB (no Docker). Config injected via env vars in `PlanoraWebAppFactory`.
- Run `dotnet test` only with dev servers stopped (the API locks `Planora.Shared.dll`).
- bUnit for Blazor components is not set up yet — add as its own feature when needed.

## Auth & Authorization

- `UserId` always from `User.FindFirstValue(ClaimTypes.NameIdentifier)` — never from request body.
- All workspace-scoped resources: verify `WorkspaceMembers` before returning data (IDOR prevention).
- Sensitive auth endpoints get `[EnableRateLimiting("auth")]`.
- Progressive lockout is manual in `AuthController.Login` — `MaxFailedAccessAttempts` is set to 100 to prevent Identity from auto-resetting the counter.

## Mapperly

`PlanoraMappingProfile` is `static partial class`. Matching property names map automatically — no manual mapper code needed.

## FluentValidation

Injected as `IValidator<T>` in constructor; called manually with `ValidateAsync` at the top of each action. Both `Create*Request` and `Update*Request` have validators (update ones are partial-update aware via `.When(x => x.Field is not null)`). Add the matching validator for any new write flow.

## Board Cover Images

Upload/delete via dedicated endpoints. `PUT /api/boards/{id}` does **not** accept `CoverImageUrl`. Magic bytes validated server-side. `BoardLimits.MaxCoverImageBytes` used by both projects. `wwwroot/uploads/boards/` is in `.gitignore`.

## Email

`IEmailSender` selects `Console` locally or `Resend` in production via `Email:Provider`. Sender is
`notifications@planora.website`. Never commit `Email:Resend:ApiKey`; use gitignored
`appsettings.Development.json` locally and the `RESEND_API_KEY` GitHub secret for deploys.
`ActivityEmailNotifier` sends workspace invites, card assignment emails, and assigned-card comment
emails while respecting profile notification preferences.

## Blazor UI

- `position:fixed` overlays must be siblings, not children, of `.board-header` or `.kanban-column` — both have `backdrop-filter` which breaks `inset:0` (creates stacking context).
- SortableJS card/column DnD requires `@key="card.Id"` and `@key="col.Id"` on foreach — without it Blazor's diffing corrupts DOM after a drag reorder.
- `planoraInitColumnsSortable` / `planoraInitCardLists` are idempotent — call every `OnAfterRenderAsync`.
- Search input is **uncontrolled** (no `value=` binding) — binding causes Blazor re-renders to overwrite typed text mid-debounce.
- Dark mode: `data-theme="dark"` on `<html>`. Kanban canvas intentionally excluded — card/column pastels need dark text.
- Board tile drag-reorder in `Workspaces.razor` uses HTML5 native DnD — not SortableJS.

## References

- [session.md](session.md) — full architecture, features, patterns, roadmap, known issues
- [docs/api-endpoints.md](docs/api-endpoints.md) — endpoint reference
