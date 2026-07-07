# Architecture

<!-- Scaffold — fill in as the system evolves -->

## Overview

Four-project .NET solution: API (ASP.NET Core), Web (Blazor WASM), Shared (DTOs/constants), and
Tests (xUnit integration tests against the API).

## Auth Flow

1. Login/Register → returns `{ token (15 min JWT), refreshToken (7 days) }`
2. `AuthHeaderHandler` attaches Bearer token; on 401 calls `/api/auth/refresh` (SemaphoreSlim guards parallel calls)
3. `PlanorAuthStateProvider.GetAuthenticationStateAsync` proactively refreshes if token is expired before any request is made
4. Logout → `POST /api/auth/logout` (rotates SecurityStamp, revokes all refresh tokens) → clear localStorage
5. Every validated JWT is cross-checked against `user.SecurityStamp` in `OnTokenValidated`

## Authorization Model

All resources are workspace-scoped. Controllers verify membership via:
```
_db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == X && m.UserId == UserId)
```
`UserId` is always taken from `User.FindFirstValue(ClaimTypes.NameIdentifier)` — never from the request body.

## Deployment

- **API**: Azure Container Apps (env vars override appsettings)
- **Web**: Azure Static Web Apps — CI injects `API_BASE_URL` secret into `appsettings.json` at build time
- **DB**: PostgreSQL (port 5433 locally — see dev notes)

## Health Checks

- `GET /health/live` — liveness; runs no checks (process-up only) so a slow/broken DB never triggers
  a restart loop.
- `GET /health/ready` — readiness; runs `DatabaseHealthCheck` (`ApplicationDbContext.CanConnectAsync`).
  Returns 503 when the DB is unreachable, 200 when healthy. Intended for Azure Container Apps probes
  (probe wiring in the container app config is still TODO).

## Testing

- `Planora.Tests` (xUnit) boots the real API in-memory via `WebApplicationFactory<Program>` against a
  dedicated `planora_test` Postgres DB (dropped + re-migrated per run). No Docker/Testcontainers — it
  reuses the local Postgres on 5433. Config is injected via env vars in `PlanoraWebAppFactory` because
  `Program.cs` reads `Jwt:Key` inline before the host is built.

## Known Gaps

- bUnit Blazor component tests not set up yet (API integration tests exist in `Planora.Tests`)
- No rate limiting on upload endpoints
- FluentValidation only on `Create*Request`, not `Update*Request`
- SortableJS reorder + active priority filter → `evt.newIndex` is relative to filtered list, not full collection
