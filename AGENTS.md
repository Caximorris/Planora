# Planora — AGENTS.md

This file is the operating contract for Codex and other coding agents working in this
repository. It is intentionally procedural: follow it before inspecting, editing, or
declaring work complete. Repository-specific rules take precedence over generic habits.

## Response and model-routing protocol

At the beginning of every response, before doing substantive work, state:

```text
MODEL ROUTING: <recommended selector value>
Reason: <one concise reason>
```

Rate claims in the response with `[Certain]`, `[Likely]`, or `[Guessing]` before the claim.
Never begin by agreeing with the user. Lead with the most useful risk, gap, or conclusion.

### Available model tiers

Last verified: 2026-07-14. Use the selector values for the tool currently running:

- Current lightweight model — Codex: `GPT-5.6 Luna` (`gpt-5.6-luna`); Claude Code: `haiku`
- Standard development model — Codex: `GPT-5.6 Terra` (`gpt-5.6-terra`); Claude Code: `sonnet`
- Strongest reasoning model — Codex: `GPT-5.6 Sol` (`gpt-5.6-sol`); Claude Code: `fable`
- Claude Code high-capability model: `opus` (use when `fable` is unavailable)

In Claude Code, these aliases select the latest available model in each family: Haiku,
Sonnet, Opus, and Fable. In Codex, use the exact display names above. Luna is for low-risk,
repetitive work; Terra is the default for normal development; Sol is for complex reasoning
and high-risk work. If a selector no longer offers one of these values, inspect the current
selector and update this section before relying on it.

### Model routing and escalation protocol

This gate applies to every request: questions, analysis, reviews, planning, diagnostics,
backtests, and implementation. Before substantive work, classify the task and announce the
recommended model. The user changes the selector manually.

Use this routing:

- **Luna** (`GPT-5.6 Luna`, `gpt-5.6-luna`; Claude Code: `haiku`): explanations, documentation,
  extraction, classification, structured summaries, formatting, renaming, and isolated
  mechanical edits.
- **Terra** (`GPT-5.6 Terra`, `gpt-5.6-terra`; Claude Code: `sonnet`): multi-file changes,
  normal production features, tests, business-logic debugging, limited refactors, and tasks
  with several related files.
- **Sol** (`GPT-5.6 Sol`, `gpt-5.6-sol`; Claude Code: `fable`, or `opus` if unavailable):
  architecture, security, authentication or permissions, database migrations, concurrency,
  transactions, caching, queues, distributed state, infrastructure, deployment, CI/CD,
  networking, broad audits, data-loss risk, ambiguous requirements, or an unclear root cause.

Use the selector syntax for the active tool: Codex display names/IDs for Codex, and the Claude
Code aliases above for Claude Code. If an alias is unavailable, use the closest exact model
name shown by that tool's selector.

When uncertain between tiers, choose the higher tier. Do not escalate merely because a task is
long; escalate based on reasoning difficulty, ambiguity, blast radius, reversibility, security
impact, and the likelihood of incorrect architectural assumptions.

If the current model is below the recommended tier, stop immediately after classification. Do
not inspect project files, run tests or backtests, edit files, commit, push, deploy, or continue
the task. If the current model is not visible, recommend the required selector without
pretending to know the current selection.

Use this exact structure when escalation is needed:

~~~text
MODEL ESCALATION REQUIRED

Recommended model: <exact model name>

Reason:
<brief explanation of the complexity, risk, or ambiguity>

Replacement prompt:

```text
<self-contained prompt ready to paste into a new thread>
```
~~~

The replacement prompt must include the objective, relevant repository and architecture
context, likely files/directories, constraints from this file, inspected context, required
implementation steps, validation commands, acceptance criteria, risks, edge cases, an
instruction to inspect before editing, and an instruction not to commit, push, deploy, or
modify production resources unless explicitly requested.

If cohesive implementation is already in progress and model switching would lose context,
stop and generate a handoff prompt containing the original request, findings, files inspected,
files modified, commands executed, test results, unresolved questions, and the exact next step.
Never assume the next thread can see this thread's history.

## Autonomous work policy

Codex may:

- inspect the complete repository;
- create branches and worktrees;
- modify code and documentation;
- install development dependencies when justified and non-production;
- run builds, tests, linters, and local services;
- create commits;
- open draft pull requests.

Codex must not:

- push directly to `main`;
- deploy to production;
- modify production secrets or commit secrets;
- execute destructive database migrations or drop/reset shared data;
- weaken authentication, authorization, rate limiting, input validation, or security headers;
- suppress tests, analyzers, formatter errors, or security warnings to obtain a passing build;
- use `git reset --hard`, `git checkout --`, recursive deletion, or equivalent destructive
  cleanup unless the user explicitly requests that exact operation;
- alter unrelated user changes in a dirty worktree.

Ask for human approval before:

- pushing any branch, merging, deploying, or changing CI/CD or production infrastructure;
- changing production configuration, secrets, domains, identity providers, permissions, or
  billing-related resources;
- adding or applying a migration that can delete, rewrite, or irreversibly transform data;
- changing authentication, authorization, tenancy boundaries, rate limits, encryption, file
  upload policy, email delivery, or other security-sensitive behavior;
- making a dependency upgrade with a significant security, licensing, or compatibility impact;
- changing public API contracts when consumers or migration strategy are unclear;
- making broad refactors outside the stated task.

Normal local implementation, tests, generated development artifacts, and a new non-destructive
EF Core migration are in scope when the request clearly requires them and the validation below
is satisfied. Never infer approval for an external side effect from a request to edit code.

## Project commands

Run commands from the repository root unless a command changes directory explicitly. Stop all
`dotnet watch`/development servers before building or testing; the API can lock
`Planora.Shared.dll` and live servers can produce Blazor asset fingerprint mismatches.

### Restore, build, test, lint, and format

```powershell
dotnet restore Planora.slnx
dotnet build Planora.slnx
dotnet test Planora.slnx
dotnet format Planora.slnx --verify-no-changes
```

`dotnet format ... --verify-no-changes` is the repository's lint/format gate. There is no
separate JavaScript linter or `package.json` at present. Do not invent a lint command or hide a
formatter failure; if the SDK cannot run this check, report it as unresolved validation.

### Codex and Git quality hooks

Repository Codex hooks live in `.codex/hooks.json`; their single policy/validation engine is
`.codex/hooks/planora_hooks.py`. Review and trust changed project hooks with `/hooks`. Enable the
Git pre-commit entry point once per clone with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Install-PlanoraHooks.ps1
```

Run explicit lifecycle gates through the shared orchestrator:

```powershell
scripts/Invoke-PlanoraQuality.ps1 -Gate pre-migration
scripts/Invoke-PlanoraQuality.ps1 -Gate pr
scripts/Invoke-PlanoraQuality.ps1 -Gate completion
```

The classifier keeps documentation-only work in Tier 1, DevEx-only work in Tier 1/2, and reserves
Tier 3 restore/build/test/format for PR or completion gates with application/runtime changes. Do not
bypass a failed hook or fabricate a receipt. See `docs/codex-hooks.md` for event mapping, durations,
failure behavior, cache invalidation, Skill/subagent routing, and the complete execution flow.

### Run the local services

API (port 8080, controlled by `PORT`, not `launchSettings.json`):

```powershell
cd Planora.Api
$env:PORT = "8080"
dotnet watch run
```

Web (HTTP port 5076):

```powershell
cd Planora.Web
dotnet watch run
```

Use separate terminals for API and Web. For a clean build/test, stop both processes first.
Local integration tests require PostgreSQL on port `5433` and a throwaway `planora_test`
database; the test factory supplies its test configuration. Do not run tests against a
production or shared database.

### EF Core migrations

```powershell
dotnet build Planora.Api
cd Planora.Api
dotnet ef migrations add <MigrationName>
```

The API applies pending migrations on boot. Build the API cleanly before creating a migration.
Inspect the generated migration and model snapshot. A destructive migration requires explicit
human approval and a rollback/data-preservation plan.

## Architecture and boundaries

```text
Planora.Web (Blazor WASM UI)
        ↓ HTTP/JSON through typed services; browser auth state/local storage only
Planora.Shared (DTOs, enums, constants, contracts)
        ↑ referenced by Web and API; no infrastructure or persistence code
Planora.Api (ASP.NET Core endpoints and application logic)
        ↓ EF Core, Identity, email, storage, jobs
Infrastructure (PostgreSQL, email provider, file storage, background jobs)
```

- `Planora.Web/` owns pages, components, layout, browser interaction, client services, and
  presentation state. It must not access EF Core, PostgreSQL, Identity stores, filesystem
  paths, production secrets, or infrastructure services directly.
- `Planora.Api/Controllers/` owns HTTP concerns: authentication context, validation entry,
  status codes, and DTO input/output. Controllers must not contain substantial persistence or
  business rules.
- `Planora.Api/Application/` owns use-case interfaces, services, options, validators, and
  mapping. Services enforce business invariants and authorization checks required by the use
  case; keep provider-specific details behind interfaces.
- `Planora.Api/Domain/Entities/` owns domain entities and invariants. Do not couple entities to
  Blazor, HTTP, or provider SDKs.
- `Planora.Api/Infrastructure/` owns EF Core/data access, migrations, email, storage, and jobs.
  Infrastructure may implement application interfaces; application code must not depend on
  concrete providers when an interface is appropriate.
- `Planora.Shared/` is the public contract between API and Web. DTO or enum changes require
  checking both consumers, endpoint documentation, compatibility, and tests.
- `Planora.Tests/` owns integration coverage using `WebApplicationFactory<Program>` and the
  throwaway PostgreSQL test database. bUnit is not configured; add it only as an explicit
  feature.

Prefer the smallest change in the owning layer. Do not solve a backend authorization problem
with a client-only check, duplicate shared contracts in Web or API, or leak EF entities through
public endpoints.

## Security rules

- Derive `UserId` only from
  `User.FindFirstValue(ClaimTypes.NameIdentifier)`; never trust a request-body user ID.
- For every workspace-scoped read or write, verify `WorkspaceMembers` and the required role
  before loading or mutating the resource. Prevent IDORs by scoping queries as well as endpoint
  checks.
- Protect sensitive authentication endpoints with `[EnableRateLimiting("auth")]`. Preserve the
  manual progressive lockout in `AuthController.Login`; do not lower protections to make tests
  or local login easier.
- Validate every write request with its injected `IValidator<T>` at the start of the action.
  Add both create and partial-update validators for new write flows. Do not rely on client-side
  validation.
- Never log passwords, JWTs, cookies, API keys, connection strings, reset tokens, invite
  tokens, or personal data unnecessarily. Do not commit `appsettings.Development.json`, local
  secrets, `.env` files, certificates, or generated credentials.
- Production email uses `Email:Provider=Resend`; the API key is supplied through the deployment
  secret mapping, never source-controlled configuration. Local development may use the console
  sender and local-only environment/configuration.
- Validate uploaded files by size and server-side magic bytes. Use
  `BoardLimits.MaxCoverImageBytes` in both projects. Keep board uploads under the ignored
  `wwwroot/uploads/boards/` path and preserve dedicated upload/delete endpoints.
- Preserve CSRF, CORS, secure cookie, JWT, rate-limit, error-handling, and security-header
  behavior unless the task explicitly addresses that behavior and includes security tests.
- Treat external input, URLs, file names, HTML, markdown, and email content as untrusted.
  Encode output, constrain paths, and avoid SSRF, path traversal, injection, and unsafe HTML.

## Files and areas that are off-limits by default

Do not modify these unless the task explicitly requires it and the applicable approval rule is
satisfied:

- `appsettings.Development.json`, production configuration, secret stores, certificates, and
  deployment credentials;
- `.git/`, `bin/`, `obj/`, generated build output, IDE metadata, and user-local files;
- existing EF migration files or the model snapshot by hand; generate migrations with EF Core;
- `Planora.Web/wwwroot/lib/sortablejs/` (vendored third-party code); upgrade it as a deliberate,
  separately reviewed dependency change;
- deployment workflows, Docker/hosting settings, and infrastructure manifests when the task
  is not explicitly about deployment or infrastructure;
- unrelated application areas or user changes already present in the worktree.

Update `session.md`, `docs/api-endpoints.md`, or other relevant documentation when behavior,
architecture, public endpoints, or roadmap assumptions change. Documentation is not a reason to
edit secrets or generated files.

## Implementation and acceptance criteria

Before editing:

1. Read this file and the relevant sections of `session.md` and `docs/api-endpoints.md`.
2. Inspect the current implementation, tests, project files, and git status.
3. Reproduce the reported problem or establish a failing test/observable gap when possible.
4. Identify the root cause and the owning architectural layer.
5. State the narrow scope, assumptions, risks, and validation plan.

While editing:

- Make the smallest maintainable correction that addresses the root cause.
- Preserve existing user changes and avoid drive-by formatting.
- Add or update focused tests for changed behavior, including authorization and validation
  cases where relevant.
- Keep API/Web contracts synchronized and update endpoint documentation for public changes.
- Preserve Mapperly conventions: `PlanoraMappingProfile` is a `static partial class`; matching
  property names map automatically.
- For Blazor changes, follow the UI rules below and verify keyboard, loading, empty, error, and
  unauthorized states where applicable.

Before declaring a task complete:

1. Reproduce the original issue again, or verify the new behavior through focused tests or a
   documented manual check.
2. Run the relevant project build and test commands; for shared DTO/API/Web changes, build the
   full solution and run the full test suite.
3. Run `dotnet format Planora.slnx --verify-no-changes` when source files changed.
4. Inspect `git diff`, `git diff --check`, and `git status`; confirm no secrets, generated files,
   unrelated edits, or accidental API changes are present.
5. Review authorization, validation, error paths, migration safety, and performance implications
   proportional to the change.
6. Update documentation when required.
7. Report commands run, results, files changed, risks, unresolved uncertainty, and any checks
   not run because of an external dependency or approval gate.

Acceptance requires: the requested behavior works, the root cause is addressed, relevant tests
cover the change, applicable build/test/format checks pass, the diff is scoped, and no known
security regression or unresolved critical failure remains. A green build alone is not
acceptance.

## Git policy

- Inspect `git status` before editing and preserve unrelated work.
- Use a branch named `codex/<short-description>` for implementation work unless the user
  explicitly requests another branch. Use a worktree for isolation when useful.
- Make focused, reviewable commits with imperative messages. Do not commit secrets, generated
  output, unrelated changes, or failing validation knowingly.
- Codex may create commits and draft pull requests, but must not push directly to `main` or
  deploy. Ask before pushing, opening a non-draft PR, merging, or changing remote state.
- Do not rewrite published history or force-push without explicit approval.
- After staging or committing, report exactly what was staged/committed and the validation state.

## Hot reload and Blazor-specific rules

- Never run `dotnet build` while a dev server is live: it can create fingerprint mismatches and
  404s for Blazor WASM assets.
- If a C# or Razor change fails edit-and-continue, stop `dotnet watch` and restart it; do not
  use `--no-verify` or other bypasses.
- A new `@page` route requires a full restart; hot reload will not register it reliably.
- `position: fixed` overlays must be siblings, not children, of `.board-header` or
  `.kanban-column`; their `backdrop-filter` creates a stacking context that breaks `inset: 0`.
- SortableJS card/column drag-and-drop requires `@key="card.Id"` and `@key="col.Id"` on the
  respective `foreach` loops. `planoraInitColumnsSortable` and `planoraInitCardLists` are
  idempotent and should be called from every applicable `OnAfterRenderAsync`.
- Search input is uncontrolled: do not add `value=` binding that overwrites text during the
  debounce re-render.
- Dark mode uses `data-theme="dark"` on `<html>`. The kanban canvas is intentionally excluded
  so card/column pastels retain dark text.
- Board tile drag-reorder in `Workspaces.razor` uses native HTML5 drag-and-drop, not SortableJS.
- Use the `app.css` motion tokens (`--dur-1/2/3`, `--ease-out/in-out`) and existing keyframes.
  Never add entrance animations to `@key`-ed sorted lists; reordering re-inserts nodes and
  replays them. `.board-root` entrance is fade-only because transforms break fixed descendants.
  Toast dismissal remains two-phase in `ToastService`, with its 200 ms removal delay at least
  `--dur-2`.

## Stop conditions

Stop and report instead of guessing when:

- requirements conflict with this file, existing security guarantees, or data integrity;
- the root cause cannot be reproduced or narrowed after reasonable read-only investigation;
- required credentials, services, database state, or external access are unavailable;
- the change would require an approval listed above;
- tests fail for an unrelated pre-existing reason and cannot be separated safely;
- the work would expand materially beyond the requested scope;
- a migration is destructive, ambiguous, or cannot be reviewed safely.

When stopping, include evidence, what was attempted, the exact blocker, the smallest decision or
approval needed, and the next safe step. Do not claim completion with skipped critical checks.

## Project reference

Planora is a .NET 10 solution composed of ASP.NET Core API, Blazor WASM, PostgreSQL, ASP.NET
Identity, Resend email, Mapperly, FluentValidation, SortableJS (vendored), and
Blazored.LocalStorage:

```text
Planora.slnx
├── Planora.Api/     Controllers/ Domain/Entities/ Application/{Interfaces,Options,Mappers,Services,Validators} Infrastructure/{Data,Email,Jobs,Storage}/ Migrations/
├── Planora.Web/     Auth/ Pages/ Components/ Services/ Layout/ wwwroot/{css,js,lib/sortablejs}
├── Planora.Shared/  DTOs/ Enums/ Constants/BoardLimits.cs
└── Planora.Tests/   xUnit — Infrastructure/PlanoraWebAppFactory, AuthFlowTests
```

Local PostgreSQL is expected on port `5433`; local connection values belong in ignored local
configuration or environment variables, not in this file. The full architecture, feature
roadmap, and known issues are in [session.md](session.md). Endpoint details are in
[docs/api-endpoints.md](docs/api-endpoints.md).
