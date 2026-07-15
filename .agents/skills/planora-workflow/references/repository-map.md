# Planora repository map

Use this reference only when the owning area is unclear. `AGENTS.md` remains authoritative.

## Architecture

- `Planora.Web`: Blazor WASM pages, components, layout, typed HTTP services, browser auth state, CSS, and JS interop.
- `Planora.Shared`: DTOs, enums, constants, and filtering contracts consumed by API and Web.
- `Planora.Api/Controllers`: HTTP/authentication context, validation entry, status codes, and DTO boundaries.
- `Planora.Api/Application`: use-case interfaces/services, validators, options, email composition, and Mapperly mappings.
- `Planora.Api/Domain/Entities`: persistence-aware domain entities and invariants; no HTTP or Blazor dependencies.
- `Planora.Api/Infrastructure`: EF Core/PostgreSQL, email providers, storage providers, health checks, filters, and jobs.
- `Planora.Tests`: xUnit unit and API integration tests through `WebApplicationFactory<Program>`.

Dependency direction: Web -> Shared <- API; API application/domain -> infrastructure implementations; public endpoints never expose EF entities.

## Primary source routing

- Every task: `AGENTS.md`.
- Architecture, auth, frontend invariants, current deployment: `CLAUDE.md` and `session.md`.
- Public HTTP behavior: `docs/api-endpoints.md` plus current controllers/tests.
- Architecture intent: `docs/architecture.md`.
- Design, responsive, and accessibility: `docs/DESIGN_SYSTEM.md`, `docs/MOBILE_AUDIT.md`, and current rendered UI.
- Blob behavior: `docs/azure-blob-storage.md` plus current provider/filter/tests.
- Product plans: context only. Confirm whether an item is already implemented before treating it as work.

## High-risk paths

- Auth/session/lockout/2FA: `AuthController`, token services, `AuthHeaderHandler`, auth-state provider, and `Planora.Tests/Auth`.
- Workspace authorization: `IWorkspaceAccessService`, controller queries, security tests, search/calendar/notification paths.
- Contracts: `Planora.Shared`, Mapperly profile, validators, Web services, endpoint docs.
- Data model: entities, EF configurations, `ApplicationDbContext`, migrations, concurrency and soft-delete tests.
- Uploads/storage: board/card upload actions, `IFileStorage`, providers, media URL filter, storage/security tests.
- Email: email factory/layout/message, transactional service, resolver, console/Resend providers, email tests.
- Notifications: entity/controller, activity triggers, Web service/layout/page, notification and workspace tests.
- UI interactions: `Board.razor`, `Workspaces.razor`, `MainLayout.razor`, `app.css`, SortableJS adapter, modal helper.
- Deployment: GitHub workflows, Dockerfile, Compose, options binding, health endpoints, Azure docs.

## Current operational facts

- .NET 10 solution with four projects in `Planora.slnx`.
- PostgreSQL integration tests expect port 5433 and create/drop `planora_test`.
- Stop Planora `dotnet watch` processes before build/test to avoid assembly locks and WASM fingerprint mismatches.
- There is no JavaScript package manifest or separate JS lint command. Do not invent one.
- bUnit and a repository Playwright test suite are not configured.
- Production API is Azure Container Apps; Web is Azure Static Web Apps; pushes to `main` deploy automatically.
