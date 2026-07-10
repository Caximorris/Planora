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

## Email Flow

- `IEmailSender` is the provider boundary. Local/dev uses `ConsoleEmailSender`; production uses
  `ResendEmailSender`.
- `Email:Provider=Resend` requires `Email:From:Address` and `Email:Resend:ApiKey` at startup.
- Production sender: `Planora <notifications@planora.website>`.
- Register sends a verification email; forgot-password sends a reset link. Verification is optional
  for login.
- Workspace invites, card assignment notifications, and assigned-card comment notifications go
  through `ActivityEmailNotifier`, respect per-user notification preferences, and never fail the
  underlying user action if the provider is down.

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
- **Email**: Resend. GitHub Actions secret `RESEND_API_KEY` is stored in Azure Container Apps as
  `resend-api-key` and mapped to `Email__Resend__ApiKey=secretref:resend-api-key`. The API deploy also
  sets `Email__Provider=Resend`, `Email__From__Address=notifications@planora.website`, and
  `App__WebBaseUrl=https://planora.website`.

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
- ~~No rate limiting on upload endpoints~~ fixed 2026-07-10: per-user `uploads` fixed-window policy
  (15/min, `RateLimiting:UploadPermitLimit`) on cover-image + attachment uploads; `UseRateLimiter`
  moved after `UseAuthentication` so the user partition sees the principal
- SortableJS reorder + active priority filter → `evt.newIndex` is relative to filtered list, not full collection
- ~~Azure Blob storage backend~~ implemented: `BlobFileStorage` (private container + SAS reads via
  `MediaUrlResolutionFilter`) runs in production (`Storage__Provider=AzureBlob` in `deploy-api.yml`);
  local dev still uses `LocalFileStorage` on disk. See `docs/azure-blob-storage.md`
