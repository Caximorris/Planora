# Planora — CLAUDE.md

Portfolio-grade Kanban app (not a toy): workspaces → boards → columns → cards, with
members/roles, invites, comments, labels, checklists, due-date calendar, global search,
in-app notifications, dark mode, and a no-account instant demo. Real auth/security model.

## Stack

.NET 10 · ASP.NET Core Web API · Blazor WASM · PostgreSQL (Npgsql) · EF Core · ASP.NET Identity
· short JWT + rotating refresh tokens · Mapperly · FluentValidation · SortableJS (vendored) ·
Blazored.LocalStorage. Deploy: API → Azure Container Apps, Web → Azure Static Web Apps.

## Architecture map

```
Planora.slnx
├── Planora.Api/      Controllers/  Domain/Entities/  Application/{Interfaces,Services,Mappers,Validators}
│                     Infrastructure/Data/  Migrations/ (19)  wwwroot/uploads/  Dockerfile
├── Planora.Web/      Auth/  Pages/  Components/  Services/  Layout/  wwwroot/{css,js,lib,sample-data}
├── Planora.Shared/   DTOs/<Domain>/  Enums/  Constants/BoardLimits.cs   (contract between Api+Web)
└── Planora.Tests/    xUnit integration tests — Infrastructure/PlanoraWebAppFactory, AuthFlowTests
```
- Entities: `Planora.Api/Domain/Entities/` (Workspace, WorkspaceMember, WorkspaceInvitation,
  WorkspaceLabel, Board, Column, Card, CardComment, CardLabel, Checklist, ChecklistItem,
  Notification, RefreshToken, AppUser, BaseEntity).
- DbContext + migrations: `Planora.Api/Infrastructure/Data/` and `Planora.Api/Migrations/`.
- **Test project**: `Planora.Tests` (xUnit) — API integration tests via `WebApplicationFactory<Program>`
  against a dedicated `planora_test` Postgres DB (dropped + re-migrated per run; no Docker/Testcontainers).
  Solution has 4 projects.
- Deploy/infra: `Planora.Api/Dockerfile`, `docker-compose.yml`, `railway.json`,
  `.github/workflows/{deploy-api.yml, azure-static-web-apps-*.yml}`, `.github/dependabot.yml`.
- Deeper detail: [session.md](session.md), [docs/architecture.md](docs/architecture.md),
  [docs/api-endpoints.md](docs/api-endpoints.md).

## Commands (verified)

```bash
cd Planora.Api && dotnet watch run   # API  → http://+:8080  (PORT env var; NOT launchSettings)
cd Planora.Web && dotnet watch run   # Web  → http://localhost:5076
dotnet restore Planora.slnx
dotnet build   Planora.slnx          # build check — NEVER while a dev server is live (see Hot reload)
cd Planora.Api && dotnet ef migrations add <Name>   # applied automatically on API boot
dotnet test Planora.slnx             # integration tests — needs local Postgres up; NOT while a dev server is live
```
Local DB: PostgreSQL port **5433**, db `planora`, user `postgres`, pass `admin1234`.
Dev JWT key: `dev-super-secret-key-minimum-32-characters-long!!` (in gitignored `appsettings.Development.json`).
- `dotnet format Planora.slnx` — available but **no `.editorconfig`** exists (SDK defaults only); verify before relying on it.
- `dotnet test` — runs `Planora.Tests`. Needs local Postgres reachable on 5433 (it creates/drops `planora_test`).
  The API dev server locks `Planora.Shared.dll`, so stop it first (same rule as `dotnet build`).

## Critical rules (highest value — prevent breakage)

**Hot reload**
- Never run `dotnet build` while `dotnet watch` is live → WASM fingerprint mismatch → 404 on all assets.
- Razor C# change that fails ENC → kill and restart `dotnet watch`; do not work around with `--no-verify`.
- New `@page` route → full restart (hot reload won't register it).

**Shared contracts**
- Any change under `Planora.Shared/` is an Api↔Web contract — confirm **both** projects compile before committing.

## Coding rules

- Backend logic stays out of `.razor` components; components call a `Planora.Web/Services/*Service`, never `HttpClient` directly.
- Frontend API access is centralized: one service per domain (`BoardService`, `CardService`, …). Don't duplicate calls across components.
- Never bypass FluentValidation — validators are injected as `IValidator<T>` and called via `ValidateAsync`.
- Don't hand-write mappings Mapperly already covers (see Mapperly below).
- Preserve async/await (no `.Result`/`.Wait()`); flow `CancellationToken` where the surrounding code does.
- Keep services single-domain; don't create broad classes mixing unrelated domains.
- Prefer small focused diffs over rewrites.

## Backend rules

- Controllers/endpoints stay thin: validate → delegate to service → map to DTO → return.
- `UserId` always from `User.FindFirstValue(ClaimTypes.NameIdentifier)` — never from the request body.
- **IDOR**: never trust `workspaceId/boardId/columnId/cardId/memberId` from the client. Verify
  `WorkspaceMembers` membership (and role where relevant) before reading/writing — keep the check at the service/data boundary.
- Preserve Identity + token security behavior (see Auth & security).
- Never return EF entities from endpoints; use `Planora.Shared` DTOs consistently.
- Preserve card/column/board ordering + reorder logic; be deliberate with archive/unarchive semantics
  (archived rows are hidden, not deleted — keep filters consistent).

**Mapperly** — `PlanoraMappingProfile` is a `static partial class`; matching property names map
automatically, no manual code. Add a new mapping method only for genuinely non-matching shapes.

**FluentValidation** — only `Create*Request` types have validators today. If you add/modify an
`Update*` flow that needs validation, add the matching validator; don't validate ad hoc in controllers.

**Board cover images** — dedicated upload/delete endpoints; `PUT /api/boards/{id}` does **not** accept
`CoverImageUrl`. Magic-byte type check + size limit server-side (`BoardLimits.MaxCoverImageBytes`,
shared). `wwwroot/uploads/boards/` is gitignored.

## Frontend rules

- Keep components small/readable; push shared logic into `Services/` or `Components/`.
- Preserve dark mode: `data-theme="dark"` on `<html>`, persisted in localStorage. Kanban canvas is
  intentionally excluded (card/column pastels need dark text).
- Preserve Ctrl+K global search. Search input is **uncontrolled** (no `value=` binding) — binding lets
  Blazor re-renders overwrite typed text mid-debounce.
- Don't break drag-and-drop:
  - SortableJS (cards/columns) requires `@key="card.Id"` / `@key="col.Id"` on the foreach — without it
    Blazor diffing corrupts the DOM after a reorder.
  - `planoraInitColumnsSortable` / `planoraInitCardLists` are idempotent — call every `OnAfterRenderAsync`.
  - Board **tile** reorder in `Workspaces.razor` uses HTML5 native DnD, not SortableJS.
- `position:fixed` overlays must be **siblings**, not children, of `.board-header` / `.kanban-column`
  (both have `backdrop-filter`, which creates a stacking context and breaks `inset:0`).
- Watch event propagation in nested card/board/modal/drag zones (stop where clicks must not bubble).
- Only the notifications bell polls (30s). Don't add new polling loops; preserve responsive layout.

## Database rules

- Schema changes only via EF Core migrations; run `dotnet build Planora.Api` cleanly first.
- Don't hand-edit generated migrations casually; keep them PostgreSQL/Npgsql-compatible.
- Be careful with cascade deletes across Workspace → Member/Invitation/Label/Board → Column → Card →
  Comment/CardLabel/Checklist → ChecklistItem, and Notification. Verify cascade vs restrict before changing FKs.
- Preserve ordering fields (position/order columns) and their indexes. Add an index for any new
  workspace-scoped query pattern.

## Auth & security rules

- JWT access token **15 min** (`Jwt:ExpirationMinutes`); refresh token **7 days** (`Jwt:RefreshTokenDays`), rotating.
- **Refresh-token reuse detection** in `RefreshTokenService` (a revoked token used again → `RevokeAllAsync`
  for that user). Do not remove or weaken this.
- **Progressive lockout** is manual in `AuthController.Login` (Identity `MaxFailedAccessAttempts=100` on
  purpose, so Identity doesn't auto-reset the counter). Lockout begins at the **3rd** failed attempt;
  actual thresholds in code: `<3 fails → no lockout`, `3–4 → 5 min`, `5–7 → 15 min`, `8–9 → 1 h`,
  `10+ → 24 h`. Once locked, even a correct password is rejected (429) until the window elapses; a
  successful login resets the counter. Verified by `Planora.Tests/Auth/LockoutTests.cs`. Keep these
  thresholds; don't swap for Identity's built-in lockout.
- `[EnableRateLimiting("auth")]` on sensitive auth endpoints — keep it.
- SecurityStamp: rotated on logout + password change; every JWT is re-checked against `user.SecurityStamp`
  in `OnTokenValidated`. Don't bypass.
- Security headers set in `Program.cs`: HSTS, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
  `Referrer-Policy: strict-origin-when-cross-origin`, CSP `default-src 'none'; frame-ancestors 'none'`. Keep all.
- Roles Owner/Admin/Member stay explicit; invite links must not allow privilege escalation.
- Never leak another workspace's data through search, calendar, notifications, or board/card endpoints.
- Never log secrets, tokens, passwords, or refresh tokens.

## Testing expectations

- `Planora.Tests` (xUnit) exists for API integration tests via `WebApplicationFactory<Program>`.
  Config is injected through env vars in `PlanoraWebAppFactory` (Program.cs reads `Jwt:Key` inline
  before `Build()`, so `ConfigureAppConfiguration` applies too late). Tests share one booted host +
  migrated DB via the `Integration` collection; keep tests independent using unique data (e.g. GUID
  emails), not shared resets. Blazor component tests (bUnit) are not set up yet — add as its own feature.
- **Require** tests for security-sensitive changes: IDOR/access checks, auth/token/lockout flows,
  refresh-token reuse. Also cover drag/drop ordering, workspace membership/roles, search, calendar,
  and notifications when you touch them.

## Deployment notes

- Push to `main` auto-deploys: API → GHCR image → Azure Container Apps (`deploy-api.yml`);
  Web → Azure Static Web Apps. **Ask before pushing.**
- Config keys (`appsettings.json`, empty in repo — real values via env/secrets):
  `ConnectionStrings:Default`, `Jwt:{Key,Issuer,Audience,ExpirationMinutes,RefreshTokenDays}`,
  `Cors:AllowedOrigins`, `Storage:{Provider,Blob:{ConnectionString,ContainerName,PublicBaseUrl}}`
  (`Provider` defaults to `Local`; `AzureBlob` is scaffolded but **not implemented** — see
  `docs/azure-blob-storage.md`). Container Apps overrides `Cors__AllowedOrigins` from a secret.
- `appsettings.Development.json` holds real local credentials — **never commit it**. No secrets in source.

## AI collaboration rules

- Inspect the relevant files before editing; prefer minimal diffs; don't refactor unrelated areas.
- Don't replace a working pattern with new architecture unless asked. When uncertain, state the
  uncertainty and choose the smallest safe change.
- Validate narrowest-first: build the touched project, then `dotnet build Planora.slnx` if a contract
  changed. Never build while a dev server is live.
- In replies: list changed files, call out risks, and state what validation you ran. Reference paths +
  line numbers instead of pasting large files.

## Open Questions / assumptions

- Lockout thresholds are now read directly from `AuthController.Login` (lockout starts at the 3rd
  failure: `3–4→5m, 5–7→15m, 8–9→1h, 10+→24h`) and pinned by `LockoutTests.cs`. An earlier note claimed
  `<5→5m` — that was wrong; the switch (`>=10/>=8/>=5/else`) fires only once `failCount >= 3`.
- `dotnet format` works via the SDK but there's no `.editorconfig`; treat formatting output as advisory.
- Azure Static Web Apps deploy inferred from `.github/workflows/azure-static-web-apps-*.yml`; exact
  resource names live in GitHub secrets, not the repo.
