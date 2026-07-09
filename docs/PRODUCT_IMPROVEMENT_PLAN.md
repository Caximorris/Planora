# Planora — Product Improvement Plan

> Living roadmap and decision log for the actual repository on `main`. Current as of 2026-07-08:
> the foundation/security/product tasks through Resend-backed transactional email are implemented
> and tested. The older prioritization sections are retained for rationale; the task ledger and
> assumptions near the bottom are the source of truth for current status.

---

## 1. Current product assessment

Planora is already well past "demo Kanban clone" on two axes that most portfolio projects fake:
**auth/security** and **frontend craft**. What it does genuinely well:

- **Real auth model, not a toy.** 15-min JWT + rotating refresh tokens, refresh-token *reuse
  detection*, `SecurityStamp` re-checked on every request in `OnTokenValidated`, manual progressive
  lockout, rate limiting on auth endpoints, HSTS/CSP/security headers. This is the strongest part
  of the codebase and it's real, not decorative.
- **Consistent workspace-scoped authorization.** Every controller verifies `WorkspaceMembers`
  membership; `UserId` always comes from the token claim, never the body. IDOR discipline is
  applied uniformly.
- **Clean layering.** Thin controllers → services, Mapperly mapping, FluentValidation on create
  paths, DTO contracts isolated in `Planora.Shared`. Blazor components call `Services/*`, never
  `HttpClient` directly.
- **Non-trivial feature breadth that actually works together:** workspaces → boards → columns →
  cards with labels, checklists, comments, assignees, priority, due dates; drag/drop (SortableJS +
  HTML5); global Ctrl+K search; in-app notifications with polling; calendar; dark mode; and a
  no-account instant demo.
- **Operational hygiene already present:** structured JSON logging in production, correlation-ID
  middleware, a production exception handler that never leaks stack traces, migrations applied on
  boot.
- **Frontend has had a real QA pass.** `FRONTEND_SENIOR_QA.md` shows deliberate accessibility,
  responsive, and drag/drop-correctness work — not just "it renders."

This is a strong base. The gaps below are about **depth and trust**, not fixing something broken.

---

## 2. Main product gaps

Ranked by how much they undermine the "serious SaaS" impression today:

1. ~~**Durable upload storage.**~~ ✅ **Done (2026-07-09)** — `BlobFileStorage` implements
   `IFileStorage` on Azure Blob, selected by `Storage:Provider=AzureBlob`; dual-read means legacy
   `/uploads/...` covers still render and blob deletes ignore them. **Remaining:** provision the Azure
   Storage account + set `Storage__*` secrets before prod cutover (code is ready; infra is not).
2. ~~**Data export and account deletion.**~~ ✅ **Done (2026-07-08)** — `GET /api/users/export`
   (full profile + member workspaces JSON) and `POST /api/users/delete-account` (password re-auth;
   solo-owned workspaces removed with the account, shared-owned block with 409) in `AccountService`.
3. ~~**Update request validation.**~~ ✅ **Done (2026-07-08)** — `Update*Request` types now have
   FluentValidation validators (partial-update aware), wired into all six write controllers.
4. **No bUnit/component test layer.** API integration coverage is strong; Blazor UI behavior is still
   validated mostly by build/manual browser passes.
5. **Upload endpoints are not rate-limited.** File type/size/scope validation exists, but upload abuse
   controls are still a gap.
6. **Filtered drag reorder still needs a fix.** SortableJS `evt.newIndex` is relative to the filtered
   list when priority filtering is active, so reorder can send the wrong full-list position.

Everything in §3+ explains how the app got here; this list is the current remaining risk surface.

---

## 3. Recommended roadmap

### Phase 1 — Foundation & product credibility

**Goals:** stop data loss, make the app probe-able, and prove the security model with tests. Nothing
here is a flashy feature; all of it is what separates "serious" from "demo."

**Features:**
- Storage abstraction (`IFileStorage`) + Azure Blob implementation; migrate cover images off local
  disk.
- Health checks (`/health/live`, `/health/ready` with a DB check) wired to Container Apps probes.
- Test foundation: xUnit + `WebApplicationFactory`, seeded with the security-critical cases
  (IDOR, lockout, refresh reuse, membership).
- GitHub Actions CI that builds the solution and runs those tests on every PR.
- Password reset + email verification via an `IEmailSender` abstraction; console sink locally and
  Resend in production.

**Why now:** #1 and #2 are active reliability/ops defects. Tests + CI must land early because every
later phase changes security-adjacent code and you have no safety net today.

**Implementation notes:** the storage interface is the keystone — attachments (Phase 2) depend on
it. Keep `IEmailSender` a thin interface so provider choice stays behind configuration. Email
verification stays optional and login must not require `EmailConfirmed`.

**Risks:** Blob migration must keep existing `CoverImageUrl` values resolvable (dual-read during
transition). CI is low risk. Email flows touch `AuthController` — the most security-sensitive file —
so they need tests alongside.

**Validation required:** integration tests green in CI; `/health/ready` returns 503 when DB is down;
cover image survives a container restart; password-reset token is single-use and expires.

### Phase 2 — Collaboration & workflow depth

**Goals:** make Planora feel *alive* and useful for a team, without drifting toward Jira.

**Features:**
- Card attachments (built on Phase-1 storage) with type/size validation and per-workspace access.
- Activity feed / audit event model (append-only) per workspace/board/card.
- Session/device management page (list + revoke refresh-token sessions).
- Workspace settings + full member/invite management UI (revoke invite, change role, transfer
  ownership, leave workspace).
- Saved filters / favorites / recently-viewed (frontend-heavy, low backend risk).

**Why now:** these are the highest *portfolio* signal per unit of risk once the foundation is safe.
Activity feed and attachments are the features reviewers remember.

**Implementation notes:** the activity model should be a single generic `ActivityEvent`
(actor, verb, target type/id, workspace scope, timestamp, small JSON payload) — resist per-feature
tables. Notifications can later be *derived* from activity instead of hand-written strings.

**Risks:** attachments = untrusted uploads → magic-byte validation, size caps, no execution, scoped
access (reuse the board-cover validation pattern already in the codebase). Transfer-ownership must
not allow privilege escalation.

**Validation required:** attachment access denied cross-workspace (test); activity event emitted on
create/move/comment; revoking a session invalidates that refresh token immediately.

### Phase 3 — Reliability, auditability & operations

**Goals:** demonstrate senior backend/product thinking about data safety and running the thing.

**Features:**
- Soft-delete + trash/restore for boards and cards; safe (recoverable) workspace deletion.
- Optimistic concurrency (Npgsql `xmin` as a concurrency token) on cards/columns/boards.
- Background cleanup job (hosted service) for expired refresh tokens and invitations.
- Data export (workspace → JSON) and account deletion (with ownership-transfer precondition).
- Notification preferences + optional email notifications (reuses `IEmailSender`).

**Why now:** these deepen the story after the app is credible and collaborative. They're less
visually impressive but score high with technical reviewers.

**Risks:** concurrency and soft-delete change query filters everywhere — must keep archive/soft-delete
filters consistent (CLAUDE.md already warns about archived-row filter consistency). Do it behind
tests written in Phase 1.

**Validation required:** conflicting concurrent update returns 409, not a lost write; trashed board
restorable within retention window; export excludes other workspaces' data.

### Phase 4 — SaaS readiness & polish

**Goals:** the finishing 10% that makes it look shipped.

**Features:**
- 2FA/TOTP + recovery codes (columns already exist).
- Toast system, error-boundary UI, empty states, onboarding checklist, unsaved-changes warning.
- Keyboard-shortcuts help modal; deep links to cards; command-menu actions (not just search).
- Terms/privacy pages, data-retention note.
- **Deliberately excluded:** billing, real-time SignalR presence, Gantt, sprints (see §6).

**Why now:** polish last, on top of a stable product. These are cheap individually and collectively
lift perceived quality.

**Risks:** low. Mostly frontend; the main hazard is scope creep — timebox it.

**Validation required:** axe/keyboard pass on new modals; TOTP enroll/verify/recovery tested;
error boundary catches a thrown component without white-screening the app.

---

## 4. Prioritized feature table

Impact legend: **H/M/L**. Complexity/Risk: **H/M/L**.

| Priority | Feature | User value | Portfolio value | Complexity | Risk | Backend | Frontend | Database | Tests required | Recommendation |
|---|---|---|---|---|---|---|---|---|---|---|
| **P0** | Blob storage abstraction (fix ephemeral uploads) | H | H | M | M | H | L | M (migrate URL) | Storage + cross-workspace access | **Build first** |
| **P0** | Health checks + probes | M | M | L | L | M | – | – | Ready=503 on DB down | **Build first** |
| **P0** | Test foundation (auth/IDOR/lockout/refresh) | – | H | M | L | M | – | – | The tests themselves | **Build first** |
| **P1** | GitHub Actions CI (build+test gate) | – | H | L | L | – | – | – | Pipeline runs on PR | Build early |
| **P1** | Password reset + email verification (`IEmailSender`) | H | H | M | M | H | M | L | Token single-use/expiry | Build |
| **P1** | Card attachments | H | H | M | M | H | M | M | Type/size/scope | Build (after storage) |
| **P1** | Activity feed / audit event model | H | H | M | M | H | M | M | Event emitted + scoped | Build |
| **P1** | Session/device management + revoke | M | H | L | L | M | M | – | Revoke invalidates token | Build |
| **P1** | Workspace settings + member/invite UI (revoke, transfer, leave) | H | H | M | M | M | H | S | Role/ownership guards | Build |
| **P1** | Soft-delete + trash/restore | H | H | M | M | H | M | M | Restore + filter consistency | Build |
| **P2** | Optimistic concurrency (`xmin`) | M | H | M | M | M | L | S | 409 on conflict | Worthwhile |
| **P2** | Background cleanup job (tokens/invites) | L | M | L | L | M | – | – | Job removes expired only | Worthwhile |
| **P2** | Notification preferences + email notifications | M | M | M | M | M | M | M | Respects prefs | Worthwhile |
| **P2** | Saved filters / favorites / recent items | M | M | M | L | M | H | S | Persist + isolation | Worthwhile |
| **P2** | Data export + account deletion | M | H | M | M | H | M | – | No cross-tenant leak | Worthwhile |
| **P2** | Toast / error-boundary / empty states | M | M | L | L | – | H | – | Component render | Polish |
| **P2** | 2FA/TOTP + recovery codes | M | H | M | M | H | M | S | Enroll/verify/recovery | Later |
| **P3** | @mentions in comments | M | M | M | L | M | M | – | Parse + notify | Optional |
| **P3** | Comment edit/delete | M | L | L | L | M | M | – | Author-only | Optional |
| **P3** | Deep links to cards / command actions | M | M | L | L | L | M | – | Route resolves | Optional |
| **P3** | Keyboard-shortcut help modal | L | L | L | L | – | M | – | – | Optional |
| **Avoid** | Stripe billing | L | M | H | H | H | H | H | Heavy | See §6 |
| **Avoid** | Gantt / timeline / sprints | L | M | H | M | H | H | H | Heavy | See §6 |
| **Avoid** | Real-time SignalR presence/cursors | M | M | H | H | H | H | – | Heavy | See §6 |
| **Avoid** | AI assistant / plugin marketplace / native mobile | L | L | H | H | H | H | H | Heavy | See §6 |

---

## 5. Recommended top 10 features (build these next)

Exactly ten, in build order.

### 1. Blob storage abstraction — fix ephemeral uploads (P0)
**Why top 10:** it's a live data-loss bug on Container Apps and a hard dependency for attachments.
**MVP scope:** `IFileStorage` (put/get/delete/url) + Azure Blob impl + local-disk impl for dev;
migrate board cover upload/delete to it; keep serving existing `CoverImageUrl`s via dual-read.
**Don't build yet:** CDN, image thumbnailing, signed-URL rotation.
**Acceptance:** cover image uploaded in prod survives a container restart; dev still works on disk.
**Tests:** upload rejects bad magic bytes/oversize; blob delete on board delete; cross-workspace
fetch denied.

### 2. Health checks + Container Apps probes (P0)
**Why:** without a real probe target, a wedged app isn't recycled.
**MVP scope:** `/health/live` (process) and `/health/ready` (DB `CanConnect`); map probes.
**Don't build yet:** dependency dashboards, per-service checks beyond DB.
**Acceptance:** `/health/ready` → 503 when DB down, 200 when up.
**Tests:** integration test toggling DB availability.

### 3. Test foundation: auth & permission integration tests (P0)
**Why:** the security model is the app's headline; nothing proves it today.
**MVP scope:** xUnit + `WebApplicationFactory` against a test Postgres (Testcontainers or a disposable
DB); cover IDOR (cross-workspace 403/404), progressive lockout thresholds, refresh-token reuse →
revoke-all, membership required on board/card endpoints.
**Don't build yet:** full component/E2E coverage.
**Acceptance:** the above behaviors are asserted and green.
**Tests:** these *are* the deliverable.

### 4. GitHub Actions CI gate (P1)
**Why:** tests without a gate rot; a CI badge is table-stakes portfolio signal.
**MVP scope:** workflow: restore → build `Planora.slnx` → run tests, on PR + push to main.
**Don't build yet:** coverage gates, matrix builds, release automation.
**Acceptance:** PR shows passing checks; red on a broken test.
**Tests:** N/A (it runs #3).

### 5. Password reset + email verification (P1)
**Why:** a SaaS without account recovery reads as unfinished; complements the strong auth core.
**MVP scope:** `IEmailSender` (console dev sink), Identity token providers for reset + confirm,
endpoints + rate limiting, minimal Blazor pages. Verification optional-but-encouraged, not enforced.
**Don't build yet:** full email template system or marketing/broadcast email.
**Acceptance:** reset token is single-use, expires, rotates `SecurityStamp` on password change; email
confirmation flips `EmailConfirmed` without blocking login for unverified accounts.
**Tests:** reset happy path, expired/replayed token rejected, lockout interaction, confirm-email happy
path and invalid token rejection.

### 6. Card attachments (P1)
**Why:** highest "real product" signal; reuses #1 directly.
**MVP scope:** `CardAttachment` entity, upload/list/delete endpoints, size/type validation, render
in card modal.
**Don't build yet:** inline image previews, drag-to-upload, versioning.
**Acceptance:** attach/download/delete within a workspace; denied cross-workspace.
**Tests:** validation, scope, cascade delete with card.

### 7. Activity feed / audit event model (P1)
**Why:** senior backend signal; a reusable event spine that notifications can later derive from.
**MVP scope:** one `ActivityEvent` table (actor, verb, targetType, targetId, workspaceId, payload
JSON, createdAt); emit on card create/move/comment/label; board-level feed view.
**Don't build yet:** per-user digests, filtering UI, retention policies.
**Acceptance:** creating/moving a card produces a scoped event; feed shows newest first.
**Tests:** event emitted on each tracked action; feed never leaks other workspaces.

### 8. Session/device management + revoke (P1)
**Why:** low risk, high trust signal, and the `RefreshToken` table already models it.
**MVP scope:** list active refresh sessions (created, expires, current-flag), revoke one / revoke
others; page in profile/settings.
**Don't build yet:** device fingerprinting, geo/IP enrichment.
**Acceptance:** revoking a session makes its refresh token unusable immediately.
**Tests:** revoke invalidates; can't revoke another user's session.

### 9. Workspace settings + member/invite management UI, with the missing verbs (P1)
**Why:** the app *has* roles and invites but can't revoke an invite, transfer ownership, or leave —
the obvious holes a reviewer clicks into.
**MVP scope:** settings page; member list with role change + remove (APIs exist); **new** endpoints:
revoke invitation, transfer ownership (Owner-only, atomic), leave workspace (blocked for sole Owner).
**Don't build yet:** granular per-board permissions, guest users, audit of role changes (comes via #7).
**Acceptance:** Owner can transfer then leave; revoked invite link stops working.
**Tests:** ownership transfer authorization, sole-owner-can't-leave, revoked token rejected.

### 10. Soft-delete + trash/restore (P1)
**Why:** data safety and reversibility — exactly the "senior" instinct; current deletes are
irreversible cascades.
**MVP scope:** soft-delete flag + `DeletedAt` on boards and cards; global query filter; trash view;
restore; make workspace deletion recoverable within a retention window.
**Don't build yet:** trash for every entity type, automatic purge scheduling (pair later with #P2 job).
**Acceptance:** deleted board hidden everywhere but restorable; filters stay consistent with archive.
**Tests:** restore path; soft-deleted rows excluded from all list/search/calendar queries.

---

## 6. Features to avoid for now

Not because they're bad — because their cost/impression ratio is wrong for this project *right now*.

- **Stripe billing / plans / usage limits.** Huge surface (webhooks, proration, tax, dunning) that,
  done half-way, looks worse than not having it. Only build if you specifically want to demonstrate
  billing — and then build *only* it, cleanly. A static pricing page is fine; live billing is not
  worth it here.
- **Full Gantt / timeline / sprint / milestone system.** This turns a focused Kanban app into a
  weak Jira. High DB and UI cost, low incremental credibility. A roadmap view is a Phase-5 maybe.
- **Real-time SignalR presence / live cursors / "currently editing".** Real-time infra (backplane,
  reconnection, auth on the hub, scaling on Container Apps) is a large, fragile investment. The
  30s notification poll already covers the *need*. Do polling-based activity first; only add SignalR
  if you explicitly want it as a showcase, and scope it to board-level "someone updated this."
- **AI assistant.** Trendy, but adds a dependency and cost without addressing any real gap in a
  Kanban tool. Skip unless it's the point of the portfolio.
- **Plugin system / public marketplace.** Enormous architecture for zero current users. Avoid.
- **Native mobile app.** The responsive web UI already has a QA'd mobile experience. A native app is
  a second codebase for no portfolio gain here.
- **Enterprise SSO / SAML / SCIM.** Only meaningful with enterprise customers; complex to do
  credibly. Out of scope.
- **Custom fields / recurring cards / time tracking.** Each nudges toward Jira and dilutes focus.
  Reconsider individually only after the top 10 land.
- **Complex analytics dashboard.** Charts over thin data look like filler. Skip until there's real
  usage to analyze.

---

## 7. Technical enablers (build before/with the features)

These unblock multiple features and prevent one-off hacks:

1. **`IFileStorage` abstraction** — precondition for attachments; also fixes the cover-image bug.
2. **`IEmailSender` abstraction** — precondition for password reset, email verification, and email
   notifications. Keep a console/no-op dev implementation.
3. **`ActivityEvent` spine** — one generic event model that notifications, audit, and feeds all read
   from. Prevents three parallel half-features.
4. **Test harness (`WebApplicationFactory` + disposable Postgres)** — precondition for trusting every
   later change to security-adjacent code.
5. **CI pipeline** — makes the harness enforceable.
6. **Update-path validators** — ✅ **Done (2026-07-08).** `Update*Request` types now have partial-update
   FluentValidation validators, wired into all six write controllers; CLAUDE.md updated accordingly.
7. **Optimistic concurrency token (`xmin`)** — enabler for safe concurrent edits/reorders.
8. **Hosted background-service pattern** — one `BackgroundService` host for cleanup jobs, reused by
   token/invite/trash purging.
9. **Frontend cross-cutting UI primitives** — a toast service and an error boundary that later
   features plug into instead of reinventing.

---

## 8. Suggested implementation order (next ~20 tasks)

Small, safe, agent-sized steps. `Risk` = L/M/H. Validation assumes no dev server running before any
`dotnet build` (per CLAUDE.md hot-reload rule).

> **Status (2026-07-07):** Tasks 1–6 are ✅ done locally. `/health/live` + `/health/ready` shipped;
> `Planora.Tests` (xUnit) boots the API via `PlanoraWebAppFactory` (`WebApplicationFactory<Program>`)
> against a throwaway `planora_test` Postgres DB. Security coverage added: IDOR/membership
> (`Security/WorkspaceAccessTests`), progressive lockout (`Auth/LockoutTests`), and refresh-token
> rotation + reuse detection (`Auth/RefreshTokenTests`) — 17 tests green in Debug and Release.
> `.github/workflows/ci.yml` builds + tests on PR/push with a Postgres service container. The `auth`
> rate limiter's `PermitLimit` is now config-driven (`RateLimiting:AuthPermitLimit`, default 10) so the
> shared-host test suite doesn't trip the global window. Task 7 ✅: cover-image disk I/O extracted behind
> `IFileStorage` (`LocalFileStorage`), registered in `Program.cs`, `BoardsController` refactored with no
> behavior change; `Boards/CoverImageTests` added (upload/validation/scope). 20 tests green.
> Tasks 8–9 (Azure Blob impl + dual-read) **paused** pending implementation. Task 10 ✅:
> `IEmailSender` + `ConsoleEmailSender` dev sink registered in `Program.cs`. Task 11 ✅:
> password reset request/confirm endpoints, Blazor forgot/reset pages, auth-client integration, and
> `PasswordResetTests` are in place. Task 12 ✅: email verification remains opt-in; register/resend/confirm endpoints, profile status/action, confirm page,
> and `EmailVerificationTests` are in place. Tasks 13-14 ✅: `ActivityEvent` spine, migration, card
> create/move emission, and board activity feed API/UI are in place with scoped tests. Task 15 ✅:
> session/device management lists active refresh-token sessions, marks the current session, revokes
> one session, and revokes all other sessions from Profile. Email verification remains optional. `dotnet test Planora.slnx` passes with 38 tests; full
> `dotnet build Planora.slnx` is clean. Chrome/Playwright verified register → profile sessions →
> revoke others on desktop and mobile with no horizontal overflow. Task 16 ✅: workspace invitation
> revocation, ownership transfer, and self-leave endpoints are in place with authorization guards and
> lifecycle tests. `docs/api-endpoints.md` is updated for the new auth/workspace/invitation routes.
> Task 17 ✅: dedicated `WorkspaceSettings.razor` page at `/workspaces/{id}/settings` surfaces workspace
> name/description edit, member list with Owner-only role change + Owner/Admin remove, pending-invite list
> with invite + revoke, and a danger zone (transfer ownership, leave, delete). A new
> `GET /api/workspaces/{id}/invitations` endpoint (Owner/Admin; marks stale pendings Expired) backs the
> invite list; `WorkspaceService` gained update/transfer/leave/revoke/list-invitations methods. The old
> Members modal in `Workspaces.razor` was removed in favor of a `⚙ Settings` link (no duplicate member UI).
> `dotnet test Planora.slnx` passes with 45 tests; full `dotnet build Planora.slnx` is clean.
> **Tasks 8-9 groundwork (2026-07-08):** deploy-target decision settled — **Azure Blob**. Prepared
> (not implemented): `StorageOptions` binds a `Storage` config section; `appsettings.json` carries an
> empty `Storage` block (`Provider: "Local"`); `Program.cs` selects the `IFileStorage` backend by
> `Storage:Provider` (`Local` → `LocalFileStorage`; `AzureBlob` throws until implemented);
> `docs/azure-blob-storage.md` documents the exact remaining steps (SDK package, `BlobFileStorage`,
> dual-read, Azure provisioning). Build clean; no behavior change (still local disk). Next: implement
> Task 8 (`BlobFileStorage`) + Task 9 (dual-read), or pick Task 19 (soft-delete + trash/restore).
> **Task 19 ✅ (2026-07-08):** soft-delete/trash for boards + cards. `DeletedAt` added to `Board`/`Card`
> (+ DTOs, Mapperly auto-maps); folded into the existing archive query filters as one predicate
> (`!IsArchived && DeletedAt == null`) on Board/Card/Column configs; every `IgnoreQueryFilters` read
> path (board GetById/GetActivity/Unarchive, card Unarchive, workspace board list, archived view)
> guarded so trashed rows never leak. `DELETE /api/boards|cards/{id}` now soft-delete; added
> `/restore`, `/permanent`, and `trash?workspaceId=|boardId=` endpoints (per-workspace board trash,
> per-board card trash). Migration `AddSoftDeleteToBoardsAndCards` (+ indexes). Frontend: workspace
> Trash view (Restore / Delete-forever) + soft-delete modal reworded to "Move to Trash"; per-board
> card Trash panel in board settings. `Planora.Tests/Boards/SoftDeleteTests.cs` (9 tests) pins
> trash/restore/permanent + filter consistency (search/calendar/GetById) + cross-workspace guards;
> `dotnet test Planora.slnx` green (54 tests), full build clean, and Chrome verified both trash flows
> end-to-end with no console errors. Scope: boards + cards only (workspace recoverable-delete deferred).
> **Post-task-24 hardening (2026-07-08 → 2026-07-09):** three follow-ups landed on top of the numbered
> ledger. (a) **CI email-test fix** — `Password_reset_email_warns_if_not_requested` asserted a raw
> apostrophe that `EmailLayout` HTML-encodes (`didn&#39;t`); assertion made encoding-agnostic (the
> template/encoding were correct). (b) **Data export + account deletion** (§2.2) — `IAccountService`
> /`AccountService`, `GET /api/users/export` (profile + all member workspaces, archived included,
> trashed/foreign excluded) and `POST /api/users/delete-account` (password re-auth; solo-owned
> workspaces deleted with the account, shared-owned → 409 + list). Frontend Profile "Privacy" section
> wired (export blob download, delete confirm panel). Tests: `Planora.Tests/Account/`
> (`AccountExportTests`, `AccountDeletionTests`). (c) **Update-path validators** (§2.3 / enabler #6) —
> seven `Update*RequestValidator`s (partial-update aware) wired into Boards/Cards/Columns/Workspaces/
> Labels/Checklists; `Planora.Tests/Validation/UpdateValidationTests.cs`. No new migrations.
> **Verified locally (2026-07-09):** `dotnet build Planora.slnx` clean (0 warnings) and
> `dotnet test Planora.slnx` green (112 tests) — (b) and (c) confirmed, caveat cleared.

1. ✅ **Add health checks.** Goal: `/health/live` + `/health/ready` (DB). Files: `Program.cs`,
   `Infrastructure/HealthChecks/DatabaseHealthCheck.cs`. Risk: L. Deps: none.
2. ✅ **Create test project.** Goal: `Planora.Tests` (xUnit) added to `Planora.slnx`, one trivial
   passing test. Files: new `Planora.Tests/`, `Planora.slnx`. Risk: L. Deps: none.
3. ✅ **`WebApplicationFactory` + test DB fixture.** Goal: boot API in-memory against disposable
   Postgres. Files: `Planora.Tests/Infrastructure/PlanoraWebAppFactory.cs`. Risk: M. Deps: 2.
4. ✅ **IDOR/membership tests.** Goal: assert cross-workspace board/card access is denied. Files:
   `Planora.Tests/Security/WorkspaceAccessTests.cs`. Risk: L. Validate: `dotnet test`. Deps: 3.
5. ✅ **Lockout + refresh-reuse tests.** Goal: assert thresholds + reuse rejection. Files:
   `Planora.Tests/Auth/LockoutTests.cs`, `Planora.Tests/Auth/RefreshTokenTests.cs`. Risk: L.
   Validate: `dotnet test`. Deps: 3.
6. ✅ **CI workflow.** Goal: build+test on PR/push with a Postgres service. Files:
   `.github/workflows/ci.yml`. Risk: L. Validate: green check on a PR. Deps: 2–5.
7. ✅ **`IFileStorage` interface + local-disk impl.** Goal: extract current cover-image disk I/O behind
   interface, no behavior change. Files: `Planora.Api/Application/Interfaces/IFileStorage.cs`,
   `Infrastructure/Storage/LocalFileStorage.cs`, `BoardsController`, `Program.cs`,
   `Planora.Tests/Boards/CoverImageTests.cs`. Risk: M. Validated: `dotnet build` clean; cover
   upload/validation/scope covered by tests (20 green). Deps: none.
8. ✅ **Azure Blob impl + config.** Goal: Blob-backed `IFileStorage` selected by config. Files:
   `Infrastructure/Storage/BlobFileStorage.cs`, `Program.cs` (switch arm + `Configure<StorageOptions>`),
   `Planora.Api.csproj` (`Azure.Storage.Blobs` 12.29.1), `Planora.Tests/Storage/BlobFileStorageTests.cs`.
   Risk: M. Validated: `dotnet build Planora.slnx` clean; 21 unit assertions cover blob-name/URL/
   content-type logic; full suite green (133). Anonymous blob-read (parity with static-file serving).
   **Not yet done:** Azure resource provisioning + `Storage__*` secrets (infra, outside repo). Deps: 7.
9. ✅ **Dual-read (no migration needed).** Goal: serve old disk URLs + new blob URLs. Implemented for
   free: frontend `new Uri(base, url)` passes absolute Blob URLs through and resolves legacy
   `/uploads/...` against the API; `BlobFileStorage.DeleteAsync` no-ops on legacy paths. Files:
   none new (verified against `BoardService`/`CardService` resolvers + `TryGetBlobName` tests).
   Risk: L. Validated: unit tests + design note in `docs/azure-blob-storage.md`. Deps: 8.
10. ✅ **`IEmailSender` + console dev sink.** Goal: interface + no-op/console impl registered. Files:
    `Application/Interfaces/IEmailSender.cs`, `Infrastructure/Email/ConsoleEmailSender.cs`, `Program.cs`.
    Risk: L. Validated: `dotnet build` clean; dev sink logs the email (dev-only, replace in prod). Deps: none.
11. ✅ **Password reset flow.** Goal: request/confirm endpoints (rate-limited) + Blazor pages. Files:
    `AuthController`, `Planora.Web/Pages/ForgotPassword.razor`,
    `Planora.Web/Pages/ResetPassword.razor`, `AuthService`,
    `Planora.Tests/Auth/PasswordResetTests.cs`. Risk: M. Validated: token single-use, invalid token
    rejected, reset rotates SecurityStamp/invalidates old JWTs, lockout clears, unknown accounts are not
    revealed; `dotnet test Planora.slnx` green (27 tests). Deps: 10, 3.
12. ✅ **Email verification (opt-in).** Goal: send on register, confirm endpoint, show status. Files:
    `AuthController`, `UsersController`, `Planora.Web/Pages/Profile.razor`,
    `Planora.Web/Pages/ConfirmEmail.razor`, `AuthService`, `UserService`,
    `Planora.Tests/Auth/EmailVerificationTests.cs`. Risk: M. Validated: confirmation flips
    `EmailConfirmed`, invalid token rejected, resend works, unverified users can still log in while the
    app uses configurable email delivery; `dotnet test Planora.slnx` green (31 tests) and full build
    clean. Deps: 10.
13. ✅ **`ActivityEvent` entity + migration + emit on card create/move.** Goal: append-only events.
    Files: `Domain/Entities/ActivityEvent.cs`, `ActivityEventConfiguration`, `ApplicationDbContext`,
    `20260707143508_AddActivityEvents`, `CardsController`. Risk: M. Validated: card create and
    move/reorder write scoped events with JSON payload; tests. Deps: 3.
14. ✅ **Board activity feed (API + UI).** Goal: newest-first feed for a board. Files:
    `BoardsController`, `Planora.Shared/DTOs/Activity/ActivityEventDto.cs`, `BoardService`,
    `Board.razor`, `app.css`. Risk: L. Validated: feed returns newest-first events and denies
    non-members; `dotnet test Planora.slnx` green (34 tests), full build clean. Deps: 13.
15. ✅ **Session management (list + revoke).** Goal: list refresh sessions, revoke one/others. Files:
    `AuthController`, `RefreshTokenService`, auth session DTOs, `AuthService`, `Profile.razor`,
    `app.css`, `Planora.Tests/Auth/RefreshTokenTests.cs`. Risk: L. Validated: current session is
    flagged, revoking one token invalidates that token only, revoking others keeps the current session,
    another user's session cannot be revoked, `dotnet test Planora.slnx` green (38 tests), full build
    clean, and Chrome/Playwright verified the Profile session UI on desktop/mobile. Deps: 3.
16. ✅ **Invite revocation + transfer ownership + leave workspace.** Goal: three endpoints + guards.
    Files: `WorkspacesController`, `Planora.Shared/DTOs/Workspace/TransferWorkspaceOwnershipRequest.cs`,
    `InvitationStatus`, `Planora.Tests/Workspaces/WorkspaceLifecycleTests.cs`,
    `docs/api-endpoints.md`. Risk: M. Validated: owner can transfer ownership then leave, non-owner
    cannot transfer, sole owner cannot leave, revoked invite token cannot be accepted, member cannot
    revoke invitations; focused integration tests green. Deps: 3.
17. ✅ **Workspace settings + member/invite UI.** Goal: surface members, roles, invites, the new verbs.
    Files: `Planora.Web/Pages/WorkspaceSettings.razor` (new `@page`), `Planora.Web/Services/WorkspaceService.cs`
    (update/transfer/leave/revoke/list-invitations), `Planora.Api/Controllers/WorkspacesController.cs`
    (`GET {id}/invitations`), `Planora.Web/Pages/Workspaces.razor` (Members modal → `⚙ Settings` link),
    `Planora.Tests/Workspaces/WorkspaceLifecycleTests.cs`. Risk: M. Validated: full member/invite/ownership
    lifecycle surfaced; list-invitations scoped to Owner/Admin (2 new tests); `dotnet build Planora.slnx`
    clean; `dotnet test Planora.slnx` green (45 tests). Deps: 16.
18. ✅ **Card attachments.** Goal: `CardAttachment` entity + endpoints + card-modal UI on `IFileStorage`.
    Files: `CardAttachment` entity/config + `AddCardAttachments` migration, card attachment DTOs,
    `CardLimits`, `CardsController` upload/delete endpoints, board/card permanent-delete and cleanup
    storage cleanup, `CardService`, `Board.razor` card-modal attachment panel, `app.css`,
    `Planora.Tests/Cards/CardAttachmentTests.cs`. Risk: M. Validated: member upload + card detail,
    content-type/signature rejection, nonmember upload/delete denial, permanent-delete cascade;
    `dotnet build Planora.slnx` clean; `dotnet test Planora.slnx` green (64 tests). Deps: 8.
19. ✅ **Soft-delete + trash/restore (boards, cards).** Goal: `DeletedAt` + global filter + trash view.
    Files: entities, Board/Card/Column configs, `AddSoftDeleteToBoardsAndCards` migration,
    Boards/Cards controllers, `BoardService`/`CardService`, `Workspaces.razor`/`Board.razor`,
    `SoftDeleteTests.cs`. Risk: M. Validated: soft-deleted excluded from list/search/calendar/GetById;
    restore + permanent-delete work; cross-workspace guards; 54 tests green; Chrome-verified. Deps: 3.
20. ✅ **Background cleanup job.** Goal: `BackgroundService` purging expired refresh tokens/invites (and
    old trash). Files: `Infrastructure/Jobs/DataCleanupRunner.cs` (scoped, testable),
    `DataCleanupBackgroundService.cs` (timer), `Program.cs`, `Planora.Tests/Jobs/DataCleanupTests.cs`.
    Risk: L. Validated: purges only expired tokens (`ExpiresAt < now`, preserves reuse-detection),
    expired invitations, and trash past a 30-day retention (loads boards to clean cover images, DB
    cascade handles children); keeps active/recent rows; 3 predicate tests; `dotnet test` green
    (57 tests). Interval/retention config-driven (`Cleanup:IntervalHours`=6, `Cleanup:TrashRetentionDays`=30);
    first pass fires one interval after startup so it never runs during the test host. Deps: 19.
21. ✅ **Optimistic concurrency (`xmin`).** Goal: concurrency token on card/column/board; 409 on
    conflict. Files: Board/Column/Card entities + configs, shared DTO/update contracts,
    `AddXminConcurrencyTokens` migration (snapshot-only; PostgreSQL `xmin` is a system column),
    Boards/Columns/Cards controllers, Blazor board/workspace update callers,
    `Planora.Tests/Concurrency/OptimisticConcurrencyTests.cs`. Risk: M. Validated: stale board,
    column, and card PUTs return 409 and preserve the current value; normal callers echo row
    versions; `dotnet build Planora.slnx` clean; `dotnet test Planora.slnx` green (60 tests).
    Deps: 3.
22. ✅ **Frontend primitives: toast + error boundary + empty states.** Goal: shared UI to reuse. Files:
    `Planora.Web/Services/ToastService.cs` (OnChange idiom, per-type auto-dismiss), `Components/ToastHost.razor`
    (mounted once in `MainLayout` inside `.app-shell`, `--z-toast`, `aria-live=polite`),
    `Components/EmptyState.razor` (Icon/Title/Description/Action slots, `Compact`), `Program.cs` (DI),
    `app.css` (toast + empty-state styles, theme-aware via semantic tokens, reduced-motion respected).
    `ErrorBoundary` already existed in `MainLayout` (retry action) — kept. First consumers wired:
    board trash/restore/permanent-delete now raise success/error toasts (previously silent), and the
    empty board-trash state uses `EmptyState`. Risk: L. Validated: `dotnet build Planora.Web` clean
    (no shared-contract change); Chrome DevTools live pass against the running stack — board
    trash/restore raise success toasts (correct `--color-success` border, ✓ icon, `role=status` +
    `aria-live=polite` dismiss button), toast auto-dismisses at exactly 4.0s, `EmptyState` renders in
    the Trash panel, console clean (no errors/warnings). Deps: none.
23. ✅ **2FA / TOTP + recovery codes (P2 auth hardening).** Goal: authenticator-app second factor with
    recovery codes; enroll/verify/disable + gated login. Files: `AuthController` (login split into
    `/login` + `/login/2fa` sharing one `CheckPasswordWithLockoutAsync` helper; `2fa/status|setup|enable|
    disable|recovery-codes`), 6 new `Planora.Shared/DTOs/Auth/*` (+`AuthResponse.RequiresTwoFactor`),
    `AuthService`, `Login.razor` (2FA step + recovery toggle), `Profile.razor` (enroll/recovery/disable
    panel), `app.css`, `Planora.Tests/Auth/TwoFactorTests.cs` (10 tests). **Also fixed `AuthHeaderHandler`
    to retry once after a silent refresh** — needed because enabling 2FA rotates the SecurityStamp, so
    without a retry the first authorized call after enrollment would 401. Built on Identity's authenticator
    provider — **no external TOTP lib and no EF migration** (`TwoFactorEnabled` + `AspNetUserTokens` already
    exist). QR *image* deferred (needs a vendored JS lib fetched with network access); manual key + otpauth
    link ship now. Risk: M (login path). Validated: `dotnet test Planora.slnx` green (74 tests); Chrome
    DevTools live pass — enroll (first-click, stamp rotation transparent), recovery codes shown once,
    logout→login→password→TOTP→authenticated, console clean. Deps: 3, 15.
    Note: recovery codes keep Identity's `xxxxx-xxxxx` format (not sanitized) so redemption matches.
24. ✅ **Resend production email + notification preferences.** Goal: real transactional email for
    account recovery/verification, workspace invites, card assignments, and assigned-card comments.
    Files: `EmailOptions`, `ResendEmailSender`, `ActivityEmailNotifier`, notification-preference
    fields on `AppUser` + migration `AddEmailNotificationPreferences`, `UsersController`
    `notification-preferences` endpoints, Profile notification toggles, deploy workflow Resend secret
    wiring, `EmailNotificationTests`, and `ResendEmailSenderTests`. Production sender:
    `Planora <notifications@planora.website>`. GitHub secret `RESEND_API_KEY` is copied to Azure
    Container Apps as `resend-api-key` and mapped with
    `Email__Resend__ApiKey=secretref:resend-api-key`; API startup fails fast if provider is Resend
    and required settings are missing. Risk: M (provider/deploy config). Validated:
    `dotnet build Planora.slnx`, focused Resend sender tests, and full `dotnet test Planora.slnx`
    green. Deps: 10, 11, 12, 16.

---

## 9. Definition of done — what "serious project" means for Planora

- **Product completeness:** a team can sign up, recover their account, create a workspace, invite +
  manage members (including transfer/leave), run boards with attachments, see activity, and recover
  deleted items. No obvious dead-ends (revoke invite, leave, reset password all work).
- **Security:** existing auth guarantees are *proven by tests* (IDOR, lockout, refresh reuse,
  SecurityStamp); uploads are validated and access-scoped; account recovery flows are single-use and
  rate-limited.
- **Testing:** integration tests for every security-sensitive path run in CI on every PR; new
  behavior ships with tests; drag/drop-ordering and permission changes are covered.
- **UX:** empty/loading/error states everywhere; toasts + error boundary; keyboard and mobile passes
  hold (per `FRONTEND_SENIOR_QA.md`); no white-screens on component errors.
- **Deployment:** health probes wired; CI gates merges; secrets via env/Key Vault; email delivery uses
  Resend; storage still needs durable Blob rather than ephemeral disk.
- **Observability:** structured logs + correlation IDs (present) plus health checks and a cleanup
  job; an activity/audit trail for user-facing actions.
- **Maintainability:** shared abstractions (`IFileStorage`, `IEmailSender`, `ActivityEvent`) instead
  of per-feature one-offs; update-path validators; small focused services per domain.
- **Documentation:** this plan, `architecture.md`, and `api-endpoints.md` kept current; a README that
  states the security model and how to run tests.

---

## 10. Final recommendation (blunt)

**Build next: the remaining small hardening gaps — upload rate limits and the filtered drag-reorder
bug fix.** Azure Blob storage is now implemented (`BlobFileStorage`, 2026-07-09); the only residual
storage work is provisioning the Azure account + secrets, which is infra, not code. Once the app is
pointed at a real Storage account, uploaded covers/attachments survive Container Apps
restart/deploy/scale-out.

**Then add product polish that compounds the existing base:** saved filters/recent items, deeper empty
states, and bUnit coverage for Profile/WorkspaceSettings/Board modal flows.

**Done now:** health checks, tests/CI, account recovery, email verification, Resend transactional
delivery, workspace settings/member lifecycle, activity feed, attachments, soft-delete/trash,
background cleanup, optimistic concurrency, frontend primitives, 2FA, data export + account
deletion, update-path validators, and **Azure Blob storage** (`BlobFileStorage` + dual-read; verified
locally 2026-07-09: build clean, 133 tests green — pending only Azure resource provisioning).

**Waste of time for this project right now:** Stripe billing, Gantt/sprints, real-time SignalR
presence, AI assistant, plugin marketplace, native mobile, and enterprise SSO. Each is a large,
fragile investment that — half-finished — makes the project look *less* serious, not more. If you want
one "wow" system, pick **activity feed + attachments**, not real-time or billing.

**What most improves the portfolio piece now:** the durable-storage code gap is closed
(`BlobFileStorage`); the last mile is provisioning the Azure Storage account so covers/attachments
are actually durable in prod. The security model is already proven by tests.

---

### Assumptions & uncertainties

- **Email provider:** Resend is configured for production via `RESEND_API_KEY`; sender is
  `notifications@planora.website`. Keep login independent of `EmailConfirmed` unless you intentionally
  decide to enforce verification later.
- **Invite expiration enforcement:** the accept/lookup paths mark stale pending invitations as
  `Expired`, and task 16 keeps revocation limited to still-pending invitations.
- **Storage target** assumed to be Azure Blob (matches the Azure hosting). If deployment is actually
  Railway (a `railway.json` exists), swap the impl behind `IFileStorage` — the abstraction makes this
  a one-file decision.
- **Test database** approach (Testcontainers vs. a CI Postgres service) is left open; either works
  with `WebApplicationFactory`. Testcontainers is cleaner locally but needs Docker in CI.
- Feature breadth should now be read from this task ledger plus `docs/api-endpoints.md`; older roadmap
  rationale is retained to explain why features were prioritized, not as a missing-feature list.
```
