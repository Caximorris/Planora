# Planora SaaS Product Expansion Plan

Current as of 2026-07-10, based on the repository rather than the older feature wishlist.

## 0. Repo-grounded baseline

### Sources read

- `CLAUDE.md`
- `docs/PRODUCT_IMPROVEMENT_PLAN.md`
- `docs/FRONTEND_AUDIT.md`
- `docs/DESIGN_SYSTEM.md`
- `docs/FRONTEND_SENIOR_QA.md`
- `docs/api-endpoints.md`
- `docs/architecture.md`
- `docs/azure-blob-storage.md`
- backend entities in `Planora.Api/Domain/Entities/`
- API controllers in `Planora.Api/Controllers/`
- EF context/configurations/migrations in `Planora.Api/Infrastructure/Data/` and `Planora.Api/Migrations/`
- frontend pages/components/services in `Planora.Web/`
- CI/deploy workflows in `.github/workflows/`
- tests in `Planora.Tests/`

### Current product reality

Planora is no longer just a portfolio Kanban board. The repo already has a serious base:

- Workspaces, boards, columns, cards, labels, checklists, comments, due dates, assignees, priorities, colors, board cover images, archive/trash/restore, and drag/drop.
- Workspace members with Owner/Admin/Member roles, email-matched invites, invitation expiration/revocation, ownership transfer, leave workspace, and member removal.
- Auth hardening: Identity, JWT, rotating refresh tokens, refresh-token reuse detection, manual progressive lockout, 2FA/TOTP, recovery codes, session listing/revocation, email verification, password reset, and auth/upload rate limits.
- Collaboration basics: comments, in-app notifications, email notifications for invites/assignment/assigned-card comments, notification preferences, board activity feed, card attachments.
- Data safety: Azure Blob-backed uploads with private container and signed read URLs, account export, account deletion, soft delete/trash, optimistic concurrency, cleanup job.
- Frontend quality: Blazor WASM service layer, command palette, calendar, profile/security page, workspace settings page, toast host, error boundary, dark mode, mobile rail/bottom nav, modal accessibility helper, centralized motion system.
- Operations: health endpoints, structured production logging, correlation IDs, CI with PostgreSQL service, Azure Container Apps and Azure Static Web Apps deployment.

### Current gaps that matter for SaaS expansion

- No payment, subscription, billing, or feature-gate model. This is intentional for now: charging is not part of the current roadmap.
- No workspace dashboard or workspace-level operating view.
- Search covers boards and cards only; it does not search docs, comments, attachments, files, people, templates, or command actions.
- Saved board filter views are client/localStorage-backed in `Planora.Web/Pages/Board.razor`, not workspace-shared or server-backed.
- No docs/wiki/page model.
- No workspace files library outside card attachments.
- No list/table/timeline view over the same card data.
- No custom fields, card watchers, multiple assignees, mentions, recurring tasks, or automation rules.
- Activity events exist, but the product has only a board feed and a limited event vocabulary.
- No bUnit or Playwright E2E suite; API integration coverage is strong, frontend behavior coverage is still mostly manual.
- Azure health endpoints exist; `docs/architecture.md` still calls Container Apps probe wiring TODO.

### Assumptions

- The goal is a product-quality SaaS direction and a stronger portfolio story. Charging for usage is undecided and explicitly out of scope for now.
- The target buyer is a small team that wants fewer tools, not an enterprise procurement department.
- The app should stay opinionated and lightweight. "Workspace OS" means one focused home for team work, not an infinite object database.
- Stripe, payment methods, checkout, subscription creation, billing portals, and billing webhooks are not-to-do for now. Revisit only after the product is substantially better and there is an explicit decision to charge.

## 1. Product positioning

### What Planora should become

Planora should become:

> A lightweight team workspace where small teams can manage projects, tasks, docs, files, workflows, and collaboration in one place.

The product direction should be "Trello plus the missing team workspace layer", not "Notion clone" or "Jira clone".

### Who it is for

- small product, design, engineering, agency, operations, and creator teams
- teams of roughly 2 to 25 people
- freelancers who run client projects with collaborators
- founders who need project boards, docs, files, due dates, comments, and simple workflow automation without an admin-heavy tool
- portfolio reviewers who want to see production-grade SaaS judgment in a focused codebase

### Who it is not for

- enterprises needing SAML, SCIM, data residency, legal hold, granular audit exports, and admin policy suites
- software teams needing Jira-grade sprints, velocity, backlog grooming, epics, releases, and complex reporting
- teams needing Notion-grade nested databases, formulas, rollups, relations, synced blocks, public websites, and page publishing
- teams needing full offline-first editing
- teams whose real requirement is a document editor, a file drive, or an analytics platform

### Why someone would pay

Users would pay if Planora reduces tool-switching for small teams:

- one workspace dashboard for projects, due dates, recent work, activity, and docs
- shared board views and filters that teammates can use, not just local preferences
- searchable docs/files/comments/tasks in one command center
- templates that make repeatable team work faster
- reliable attachment storage, data export, audit trail, account/security controls, and workspace administration
- small automations and reminders that reduce manual follow-up

They will not pay just because a Kanban board has prettier cards. Kanban is the table stakes.

### What makes it different from a basic Kanban clone

- Workspace-level coordination: dashboard, members, activity, files, docs, and shared search.
- Real security and trust: tested auth, workspace-scoped permissions, sessions, 2FA, export/deletion, private file URLs.
- Lightweight docs linked directly to the work, not a separate notes island.
- Shared views and templates that turn boards into repeatable systems.
- Focused automations that support the workflow without becoming a rules platform.

### What should remain intentionally simple

- Roles: keep Owner/Admin/Member until real customers prove a need for granular permissions.
- Docs: use a simple rich text/Markdown editor, not a block editor platform.
- Automations: rule templates first, not arbitrary multi-step workflows.
- Views: board/list/table/calendar first, not a full Gantt suite.
- Files: attachments and workspace file library, not a Google Drive replacement.
- Analytics: useful board summaries, not BI dashboards.

### What should never be built unless the product grows significantly

- plugin marketplace
- full Notion block editor
- enterprise SSO/SAML/SCIM
- native mobile apps
- public page publishing
- complex Gantt and portfolio planning
- full Jira sprint system
- full offline mode
- arbitrary webhook/action marketplace
- AI assistant as a headline feature

### Blunt Notion-level assessment

"Notion-level" is not realistic or desirable for Planora. Notion is a document/database platform with years of editor, sync, permissions, publishing, import/export, and collaboration complexity. Building a weak version would damage Planora's focus and maintenance profile.

The focused alternative is better:

- Notion-inspired docs, templates, mentions, favorites, recent items, and workspace search.
- No full block editor, no public websites, no formulas/relations/rollups, no arbitrary databases.
- Treat docs as supporting project work, not as the product's center of gravity.

## 2. Charge-optional product model

### Payment and subscription recommendation

Do not implement Stripe, payment methods, subscription creation, checkout, billing portals, billing webhooks, invoices, plan purchase flows, or any live monetization surface now.

The roadmap should improve the product first: workspace dashboard, shared saved views, docs/wiki, advanced search, templates, custom fields, workspace files, mentions/watchers, and automations. After those improvements exist, decide whether charging for usage is even desirable.

Allowed now:

1. Track usage when it protects reliability or storage safety.
2. Keep product packaging ideas as planning notes only.
3. Preserve local/demo mode without any charge friction.
4. Build features because they improve Planora, not because they need a paywall.

Not-to-do now:

1. Stripe or any payment provider.
2. Subscription entities and subscription lifecycle.
3. Checkout, customer portal, invoices, webhooks, proration, dunning, tax, coupons, trials, or paid seats.
4. Hard paywalls or upgrade prompts inside the portfolio/demo experience.

### Hypothetical packaging only, not an implementation plan

| Hypothetical package | Intended user | Workspaces | Members | Boards | Storage | Attachments/files | Activity retention | Automations | Templates | Search | Views/custom fields | Support |
| - | - | - | - | - | - | - | - | - | - | - | - | - |
| Free | trial, portfolio demo, solo/light teams | 1 active workspace | 3 members | 5 active boards | 250 MB | card attachments only, 10 MB/file | 14 days | none except built-in reminders | sample templates only | cards/boards | local saved filters only | docs/FAQ |
| Pro | solo operators, freelancers | 3 workspaces | 5 members/workspace | 25 boards/workspace | 5 GB/user | card attachments + workspace files | 90 days | simple due/assignment reminders | personal/team templates | cards/boards/comments/docs | server saved views, list view | email support |
| Team | small teams | unlimited workspaces within account/team | 25 members/workspace | unlimited practical limit | 20 GB/workspace + 5 GB/member | workspace files, previews, quotas | 1 year | rule templates with monthly run limit | workspace templates | advanced ranked search | list/table/calendar, custom fields | priority support |
| Business | larger teams if charging is chosen later | unlimited | 100 members/workspace | unlimited practical limit | 100 GB/workspace + add-ons | larger files, export bundles | 3 years or configurable | higher limits + history | shared template library | advanced filters/saved searches | advanced permissions, audit/export | priority/SLA-ish support |

These tiers are only a thinking tool for future packaging. Do not create plan purchase flows, subscriptions, billing records, or payment integration for them now.

### What should be free

- instant demo with no charge friction
- register/login/recovery/security basics
- one workspace with enough boards/cards to evaluate the product
- core Kanban: columns, cards, labels, checklists, comments, due dates, assignees, priority, colors
- basic board/card search
- limited attachments
- basic calendar
- profile, email verification, password reset, 2FA, session revoke

### What could become premium only if charging is chosen later

- shared server-backed saved views
- docs/wiki beyond a tiny free allowance
- workspace files library and higher storage quotas
- advanced search across comments/docs/files
- custom fields
- list/table views
- templates beyond sample templates
- automation rules
- longer activity/audit retention
- workspace exports
- advanced member permissions later

### What should be usage-limited

- storage bytes
- file size
- active members
- active boards
- active docs/pages
- custom fields per workspace/board
- automation rules and monthly runs
- activity retention window
- export job frequency/size

### What should remain unlimited for portfolio/demo value

- public landing page and instant demo account
- local/dev mode feature access
- ability to create and edit core board/card objects in the demo
- security/account features
- enough seeded demo data to show the product without setup

### Features that would justify charging only if that decision is made later

The strongest future commercial bundle would be:

- shared views + multiple views
- docs/wiki + templates
- workspace files + storage quotas
- advanced search/command center
- custom fields
- mentions/watchers + notification control
- automations + recurring cards
- workspace export and longer audit/activity history

Billing for "more boards" alone is weak. Billing for "a real team workspace layer" is credible.

## 3. Core product expansion pillars

### A. Workspace operating system

| Feature | Recommendation | Why |
| - | - | - |
| Workspace dashboard | P1 | Gives every workspace a home: recent boards, assigned cards, due soon, activity, docs, files. |
| Workspace settings | Mostly done | `WorkspaceSettings.razor` exists; continue hardening it instead of replacing it. |
| Member management | Mostly done | Role change/remove/transfer/leave exist; add clearer permission UX and tests as new features touch roles. |
| Invite expiration/revocation | Done | Present in endpoints and docs. Keep audit events for invite lifecycle. |
| Transfer ownership | Done | Present. Keep it central to account deletion and team trust. |
| Leave workspace | Done | Present with Owner guard. |
| Remove member | Done | Present. Add activity/audit event emission. |
| Role management | P1 hardening | Keep three roles; document what Admin can/cannot do. |
| Workspace activity feed | P1 | ActivityEvent exists but needs workspace-level feed, richer verbs, and retention policy. |
| Workspace-level search | P1 | Search currently boards/cards globally; add scoped search and new result types. |
| Workspace templates | P2 | Useful once board/card/doc templates exist. |
| Workspace onboarding checklist | P1 | High activation value; no heavy backend required for MVP. |

### B. Project management depth

| Feature | Recommendation | Why |
| - | - | - |
| Saved views | P1 | LocalStorage views exist; server-backed shared views are a strong Team feature. |
| Advanced filters | P1 hardening | Current board filters are good; persist/share them and support search URLs. |
| Sorting | P1 | Needed for list/table views and saved views. |
| Grouping | P2 | Useful after list/table; keep first grouping to status/assignee/priority. |
| Custom fields | P1/P2 | Strong product value; start with text/number/date/select/checkbox. |
| Card templates | P1 | Repeated work is a major small-team need. |
| Board templates | P1 | High activation and future commercial value. |
| Recurring cards | P2 | Valuable, but needs background jobs and careful duplicate prevention. |
| Dependencies/blockers | P2 | Useful but can quickly become Jira-ish; start with one-to-many blockers only. |
| Start date + due date | P2 | Needed for timeline later; not urgent before list/table. |
| Multiple assignees | P2 | Useful for collaboration; increases notification and UI complexity. |
| Card watchers | P1 | Better than multiple assignees as a first collaboration primitive. |
| Bulk actions | P2 | Useful after list/table views. |
| Board analytics | P3 | Keep to simple counts/cycle indicators; avoid dashboards. |
| Timeline view | P3 | Delay until start date, custom fields, and list/table are stable. |
| List view | P1 | Best next view; low conceptual overhead. |
| Table view | P1 | High product value with custom fields. |
| Calendar improvements | P2 | Add filters, drag due dates later, scoped workspace/board switching. |

### C. Lightweight docs/wiki system

Planora should add docs, but only as lightweight project documentation.

| Feature | Recommendation | Why |
| - | - | - |
| Workspace docs | P1 | Core workspace-complete pillar. |
| Board docs | P1 | Project brief/spec/meeting notes attached to a board. |
| Card-linked docs | P2 | Useful for specs, but avoid putting documents inside every card initially. |
| Simple rich text editor | P1 | Needed for usability. Use a proven editor package rather than inventing editing behavior. |
| Markdown support | P1 | Low-cost export/import and technical-user friendliness. |
| Document folders/pages | P1 | Simple tree or flat sections. Avoid deeply nested databases. |
| Mentioning cards inside docs | P2 | Valuable once search/indexing exists. |
| Linking docs to boards/cards | P1 | The key Notion-inspired behavior for Planora. |
| Templates for meeting notes/project briefs/specs | P1 | High activation and product value. |
| Search docs through Ctrl+K | P1 | Docs are only useful if findable. |
| Permissions inherited from workspace | P1 | Simpler and safer than per-page permissions. |
| Version history | P2/P3 | Start with updated timestamps; add snapshot history later if docs become central. |
| Full Notion block editor | Avoid | Maintenance trap and not necessary for Planora's positioning. |

### D. Files and attachments

| Feature | Recommendation | Why |
| - | - | - |
| Card attachments | Done | Entity, endpoints, Blob storage, UI, tests exist. |
| Board attachments | P2 | Useful for project files; can reuse attachment model. |
| Workspace files | P1 | Strong "workspace complete" feature and plan/storage value. |
| Azure Blob Storage integration | Done | Private container + SAS URL flow exists. |
| Secure file access by membership | Done for current files | Keep using API authorization + `MediaUrlResolutionFilter`; do not leak raw stored URLs. |
| Upload validation | Done for current upload types | Extend allowlists per file feature. |
| File type restrictions | P1 hardening | Make restrictions configurable per plan/use. |
| File size limits | Done/P1 | Current card limit exists; add per-plan limits. |
| Image thumbnails | P2 | Useful, but can wait until workspace files exist. |
| Cover image cleanup | Done | Existing cleanup and delete paths handle covers. |
| Attachment deletion cleanup | Done | Existing cleanup handles card attachment files. |
| Storage usage visibility | P1 | Useful for reliability and future decision-making; not a billing prerequisite now. |
| File previews | P2 | Start with images/PDF metadata; avoid full document rendering. |
| Export/download | P1/P2 | Account export exists; add workspace export bundle later. |

### E. Collaboration

| Feature | Recommendation | Why |
| - | - | - |
| Activity feed | P1 expansion | ActivityEvent exists; expand verbs and workspace feed. |
| Audit log | P2/Business | Same backbone as activity but retention/access differs. |
| SignalR real-time board updates | P3 | Valuable later, not before docs/search/templates. |
| Comment editing/deletion | P2 | Delete exists; edit is useful but not a major commercial driver. |
| @mentions | P1 | High collaboration value and notification value. |
| Email notifications | Done/P1 expansion | Provider exists; add mention/watch/digest preferences. |
| Notification preferences | Done/P1 expansion | Current email toggles are coarse; add channel/topic granularity. |
| Card watchers | P1 | Better targeted notifications and strong collaboration value. |
| Presence indicators | P3 | Nice, but not central. |
| Currently editing indicators | P3 | Requires real-time/conflict semantics; delay. |
| Reactions | Avoid/P3 | Low product value for current scope. |
| Threaded comments | Avoid/P3 | Adds UI complexity; flat comments are enough now. |

### F. Automation

| Feature | Recommendation | Why |
| - | - | - |
| When card moved to column -> notify/update field | P2 | Useful simple automation, especially with watchers. |
| Due date approaching -> notify | P1/P2 | Reminder job is simple and valuable. |
| Card assigned -> notify | Done | Current email/in-app assignment flows exist. |
| Critical priority -> notify admins/watchers | P2 | Useful template rule. |
| Recurring tasks | P2 | High small-team value but needs safe background job logic. |
| Rule builder | P3 | Delay until fixed rule templates prove demand. |
| Automation history | P2 | Needed once rules exist; important for debugging. |
| Automation usage limits | P2 | Useful to protect the system from runaway jobs; not tied to pricing now. |
| Zapier clone | Avoid | Not the product. |

### G. Search and command center

| Feature | Recommendation | Why |
| - | - | - |
| Search cards, boards, docs, comments, files | P1 | Critical workspace OS behavior. |
| Search filters | P2 | Add after more result types exist. |
| Recent items | P1 | High UX value; low conceptual cost. |
| Favorite boards/docs | P1 | High retention and navigation value. |
| Command menu actions | P1 | Turn Ctrl+K into create/go/action center. |
| Quick create card/board/doc | P1 | Strong productivity value. |
| Keyboard shortcuts | P2 | Add only for common actions. |
| Deep links to cards/docs | P1 | Needed before search feels complete. |
| Search result ranking | P2 | Start simple; improve with activity/recent/favorite signals. |

### H. Notifications and email

| Feature | Recommendation | Why |
| - | - | - |
| Email verification | Done | Present and optional. |
| Password reset | Done | Present. |
| Change email | P1 | Important account lifecycle gap. Requires confirmation for old/new email or at least new email. |
| Email notification system | Done/P1 expansion | Resend + templates exist; add digest/mentions/watchers. |
| Notification preferences | Done/P1 expansion | Current toggles are assignment/comment/invite only. |
| Digest emails | P2 | Useful after activity feed and mentions mature. |
| Mention emails | P1 | Tied to @mentions. |
| Assignment emails | Done | Present. |
| Due date reminders | P1/P2 | Valuable and automation-adjacent. |
| Invite emails | Done | Present. |
| Unsubscribe/preferences | P1 | Needed for trust; link emails to notification settings. |
| Resend integration | Done | Present and configured for production. |

### I. Account, security, and trust

| Feature | Recommendation | Why |
| - | - | - |
| Session/device management | Done | Present. |
| Revoke sessions | Done | Present. |
| 2FA/TOTP | Done | Present. |
| Recovery codes | Done | Present. |
| Account deletion | Done | Present with workspace ownership guard. |
| Export my data | Done | Present. |
| Data retention | P1 | Document and enforce retention for trash/activity/files. |
| Terms/privacy pages | P1 | Required before inviting real users, even without charging. |
| Workspace data export | P1 | Strong admin/trust feature; account export is not enough for team owners. |
| Permission test suite | P1 ongoing | Existing suite is strong; expand for every new domain. |
| Audit trail | P1/P2 | ActivityEvent exists; formalize retention and admin visibility. |
| Upload security | Done/P1 ongoing | Keep all file features on same validation path. |
| Rate limit visibility | P2 | Admin-facing rate-limit UI is not urgent; better error messages first. |

### J. Admin, operations, and reliability

| Feature | Recommendation | Why |
| - | - | - |
| Health checks | Done | `/health/live` and `/health/ready` exist. |
| Structured logging | Done | JSON logging in production exists. |
| Error tracking | P1 | Add a provider or structured sink before broader real-user use. |
| Metrics | P1 | Request rate, errors, latency, DB, background jobs, storage usage. |
| Admin dashboard | P3 | Internal-only later; not user-facing value now. |
| Background jobs | Done/P1 expansion | Cleanup exists; reminders/recurrence/export jobs can reuse pattern. |
| Cleanup jobs | Done/P1 expansion | Current cleanup handles tokens/invites/trash/files. |
| Expired invites cleanup | Done | Present. |
| Expired refresh tokens cleanup | Done | Present. |
| Orphan files cleanup | P1 | Add storage reconciliation once workspace files exist. |
| CI pipeline | Done | Present. |
| Staging environment | P1 | Needed before broader real-user use. |
| Migration strategy | P1 | Auto-migrate on boot is convenient; serious SaaS operation needs explicit rollout policy. |
| Monitoring | P1 | Required before real users. |
| Rollback strategy | P1 | Need deployment rollback notes and migration caution. |

### K. Onboarding and activation

| Feature | Recommendation | Why |
| - | - | - |
| Better demo workspace | P1 | Current seeder is useful; update it as docs/templates/files ship. |
| Guided onboarding | P1 | Workspace checklist should guide first value. |
| Empty states | P1 ongoing | Component exists; fill remaining surfaces with action-oriented states. |
| First board creation flow | P1 | Add template choice, not a blank form only. |
| Workspace setup checklist | P1 | Invite teammate, create board, add doc, upload file, save view. |
| Sample templates | P1 | Highest activation-to-complexity ratio. |
| Product tour | P3 | Lower value than contextual empty states/checklist. |
| Contextual tips | P2 | Useful if restrained. |
| Import from Trello/CSV | P2/P3 | Useful later; CSV first. |
| Invite teammates prompt | P1 | Important activation milestone. |
| Example board templates | P1 | High value and portfolio signal. |

## 4. Notion-like capabilities

Planora should be Notion-inspired only where it helps project work.

| Capability | Classification | Planora version |
| - | - | - |
| Simple docs/wiki | Build now | Workspace/board docs with title, body, owner, timestamps, simple hierarchy. |
| Pages linked to workspaces/boards/cards | Build now | Workspace docs and board-linked docs first; card links later. |
| Templates | Build now | Board, card, and doc templates. |
| Databases/custom tables | Replace with simpler version | Custom fields + table view over cards, not arbitrary databases. |
| Multiple views over same data | Build now | Board/list/table/calendar over cards. Timeline later. |
| Rich text | Build now | Simple editor or Markdown-rich hybrid. No custom block engine. |
| Mentions | Build later | Start with @user mentions in comments, then docs and card links. |
| Linked references | Build later | Backlinks between docs/cards only after docs/search. |
| Favorites/sidebar navigation | Build now | Favorites and recent boards/docs in shell/workspace dashboard. |
| Recent pages | Build now | Recent boards/docs/cards stored per user. |
| Workspace search | Build now | Search cards, boards, docs, comments, files. |
| Comments on docs | Build later | Useful after docs MVP; do not add comments to every object. |
| Page history | Build later | Snapshot history after docs are used enough to justify the maintenance cost. |
| Public share links | Avoid | Not aligned with team workspace focus; high permission/security risk. |
| Full block editor | Avoid | Too expensive and not central to Planora. |
| Formulas/rollups/relations | Avoid | Turns Planora into a weak database platform. |

## 5. Feature prioritization

Scale: user value, future commercial value, portfolio value, complexity, maintenance, and risk are Low/Medium/High. Priority meanings:

- P0: necessary for trust, security, data safety, or serious SaaS readiness.
- P1: strong product value and should be built soon.
- P2: useful differentiator, but not essential.
- P3: future polish.
- Avoid: not worth building now.

| Feature | User value | Future commercial value | Portfolio value | Complexity | Backend impact | Frontend impact | DB impact | Security impact | Testing impact | Maintenance | Risk | Priority |
| - | - | - | - | - | - | - | - | - | - | - | - | - |
| Central workspace authorization service | Medium | Medium | High | Medium | High | Low | None | High | High | Low | Medium | P0 |
| Internal plan/feature limit service | Medium | High | High | Medium | High | Medium | Medium | High | High | Medium | Medium | P0 |
| Storage usage metering | Medium | High | High | Medium | High | Medium | Medium | High | High | Medium | Medium | P0 |
| Workspace data export | High | High | High | Medium | High | Medium | Low | High | High | Medium | Medium | P0/P1 |
| Terms/privacy/data retention pages | Medium | Medium | Medium | Low | Low | Medium | None | Medium | Low | Low | Low | P0/P1 |
| Workspace dashboard | High | Medium | High | Medium | Medium | High | Low | Medium | Medium | Medium | Low | P1 |
| Workspace activity feed expansion | High | High | High | Medium | High | Medium | Low | Medium | High | Medium | Medium | P1 |
| Server-backed saved views | High | High | High | Medium | Medium | High | Medium | Medium | High | Medium | Medium | P1 |
| Favorites and recent items | High | Medium | Medium | Medium | Medium | Medium | Medium | Medium | Medium | Low | Low | P1 |
| Command center actions | High | High | High | Medium | Medium | High | Low | Medium | Medium | Medium | Medium | P1 |
| Advanced search across docs/comments/files | High | High | High | High | High | Medium | Medium | High | High | Medium | Medium | P1 |
| Lightweight docs/wiki | High | High | High | Large | High | High | High | High | High | High | Medium | P1 |
| Board/card/doc templates | High | High | High | Medium | High | High | Medium | Medium | High | Medium | Medium | P1 |
| Workspace files library | High | High | High | Medium | High | High | Medium | High | High | Medium | Medium | P1 |
| List view | High | Medium | Medium | Medium | Medium | High | Low | Medium | Medium | Medium | Low | P1 |
| Table view | High | High | High | Large | Medium | High | Low | Medium | Medium | Medium | Medium | P1 |
| Custom fields MVP | High | High | High | Large | High | High | High | High | High | High | High | P1/P2 |
| @mentions | High | High | High | Medium | High | High | Medium | Medium | High | Medium | Medium | P1 |
| Card watchers | High | High | Medium | Medium | High | Medium | Medium | Medium | High | Medium | Medium | P1 |
| Change email flow | Medium | Medium | Medium | Medium | High | Medium | Low | High | High | Low | Medium | P1 |
| Due date reminders | High | Medium | Medium | Medium | High | Medium | Low | Medium | Medium | Medium | Medium | P1/P2 |
| Recurring cards | High | High | High | Large | High | High | Medium | Medium | High | High | Medium | P2 |
| Simple automations | High | High | High | Large | High | High | High | High | High | High | High | P2 |
| Board analytics summary | Medium | Medium | Medium | Medium | Medium | Medium | Low | Low | Medium | Medium | Low | P2/P3 |
| SignalR real-time board updates | Medium | Medium | High | Large | High | High | Low | High | High | High | High | P3 |
| Timeline view | Medium | Medium | Medium | Large | Medium | High | Medium | Medium | Medium | High | Medium | P3 |
| Full Notion editor | Low | Low | Medium | XL | High | High | High | High | High | Very high | High | Avoid |
| Enterprise SSO/SAML | Low now | Medium later | Medium | XL | High | Medium | Medium | High | High | High | High | Avoid |
| Native mobile app | Low now | Low | Medium | XL | Medium | XL | None | Medium | High | Very high | High | Avoid |

## 6. Top 15 charge-worthy product features

Exactly 15 features selected because they make Planora valuable enough that charging could be reconsidered later. They are not a mandate to add payments or subscriptions.

### 1. Workspace dashboard

- Description: A workspace home showing assigned cards, due soon, recent boards/docs/files, activity, and onboarding checklist.
- Why users value it: It turns scattered boards into a team operating center.
- MVP scope: one dashboard page, summary endpoint, recent activity, due cards, recent boards, setup checklist.
- What not to build yet: customizable widgets, analytics suite, admin dashboard.
- Backend work: `WorkspacesController`, new query DTOs in `Planora.Shared/DTOs/Workspace`, maybe dashboard service.
- Frontend work: new section/page under `Planora.Web/Pages/Workspaces.razor` or a route like `/workspaces/{id}`.
- Database work: none for MVP unless checklist state is persisted.
- Security/permissions: member-gated; no cross-workspace aggregation leaks.
- Tests required: dashboard denies nonmembers; due cards/activity are scoped; empty workspace returns safe defaults.
- Acceptance criteria: workspace opens to useful recent/due/activity data within one request; no foreign workspace items.
- Estimated complexity: M.
- Risk: Low.
- Phase: Phase 2.

### 2. Server-backed saved board views

- Description: Persist filter/sort/group view definitions per board or workspace, shareable with teammates.
- Why users value it: Teams reuse the same operating views instead of each user rebuilding filters.
- MVP scope: save/update/delete views for a board; filters from `Planora.Shared/Filtering`; owner vs workspace-shared flag.
- What not to build yet: permissions per view, cross-board dashboards.
- Backend work: new `SavedView` entity/controller/service; reuse `BoardFilterState`.
- Frontend work: replace localStorage-only saved views in `Board.razor` with API-backed views and keep local fallback for dev errors.
- Database work: `SavedViews` with workspace/board/user indexes and JSON payload.
- Security/permissions: creator must be workspace member; shared views visible only to workspace members.
- Tests required: CRUD, cross-workspace denial, invalid JSON/schema rejection, local migration behavior if needed.
- Acceptance criteria: saved views persist across browsers and can be shared with workspace members.
- Estimated complexity: M.
- Risk: Medium.
- Phase: Phase 2.

### 3. List and table views over cards

- Description: Alternate board data views with columns for title, status, assignee, priority, due date, labels, and later custom fields.
- Why users value it: Many team workflows need scan/sort/edit, not just Kanban.
- MVP scope: list view first, then table view; read from existing board detail data; update card fields inline where safe.
- What not to build yet: spreadsheet formulas, frozen columns, bulk import editing.
- Backend work: optional card query endpoint for efficient view data; existing card update endpoints can serve MVP edits.
- Frontend work: new view switcher in `Board.razor`, list/table components.
- Database work: none for base fields.
- Security/permissions: all edits use existing card permission checks and row version.
- Tests required: update conflict handling, filters/sorts, no archived/trashed leakage.
- Acceptance criteria: users can switch views without losing filters and edit common fields safely.
- Estimated complexity: L.
- Risk: Medium.
- Phase: Phase 3.

### 4. Custom fields MVP

- Description: Per-board fields of type text, number, date, select, checkbox attached to cards.
- Why users value it: Custom fields make Planora fit real team workflows without becoming Jira.
- MVP scope: board-level field definitions; card values; render/edit in card modal and table view.
- What not to build yet: formulas, relations, rollups, cross-board fields, field-level permissions.
- Backend work: `CustomField`, `CustomFieldOption`, `CardCustomFieldValue`; validators and endpoints.
- Frontend work: board settings field manager, card modal editors, table columns.
- Database work: new tables with board/card indexes; JSON value is tempting but typed columns or value tables test better.
- Security/permissions: board workspace membership; Admin/Owner manage definitions; members edit values.
- Tests required: type validation, option deletion behavior, cross-workspace denial, export inclusion.
- Acceptance criteria: field definitions and values survive reload, validate by type, and appear in table view.
- Estimated complexity: L.
- Risk: High.
- Phase: Phase 3.

### 5. Lightweight docs/wiki

- Description: Workspace and board documents with simple rich text/Markdown content.
- Why users value it: Teams can keep project briefs, meeting notes, specs, and decisions next to tasks.
- MVP scope: docs list, create/edit/archive/delete, title/body, board link, workspace inherited permissions.
- What not to build yet: block editor, public pages, nested databases, real-time collaborative editing.
- Backend work: `Document` entity, docs controller, validators, mapping, search indexing hooks.
- Frontend work: docs list/page editor, board-docs panel, command palette integration.
- Database work: `Documents` table with workspace/board/updated indexes and soft-delete.
- Security/permissions: workspace-scoped access; sanitize or safely render rich text/Markdown.
- Tests required: CRUD, XSS/sanitization expectations, cross-workspace denial, search inclusion.
- Acceptance criteria: users can create a project brief linked to a board and find it through Ctrl+K.
- Estimated complexity: L.
- Risk: Medium.
- Phase: Phase 3.

### 6. Board, card, and doc templates

- Description: Reusable templates for common boards, cards with checklists/labels, and docs.
- Why users value it: Templates save setup time and make repeatable work feel professional.
- MVP scope: built-in templates plus workspace templates; create board/card/doc from template.
- What not to build yet: template marketplace, versioned template releases, public sharing.
- Backend work: `Template` entity or seeded JSON plus endpoints; copy logic.
- Frontend work: template picker in new board/card/doc flows and onboarding.
- Database work: template table if user-created templates are included.
- Security/permissions: workspace templates are workspace-scoped; only Admin/Owner manage shared templates.
- Tests required: template copy preserves intended fields only; no cross-workspace references leak.
- Acceptance criteria: user can create a "Project kickoff" board/doc/card from a template in under a minute.
- Estimated complexity: M.
- Risk: Medium.
- Phase: Phase 3.

### 7. Workspace files library

- Description: A workspace-level file area using existing `IFileStorage`, with metadata and secure downloads.
- Why users value it: Files become findable project context rather than hidden card-only attachments.
- MVP scope: upload/list/delete workspace files, file metadata, search by filename, storage quota accounting.
- What not to build yet: folder sync, office editing, versioning, full preview renderer.
- Backend work: `WorkspaceFile` or generalized `Attachment`; endpoints; quota checks.
- Frontend work: files page/panel, upload progress, delete/download, empty states.
- Database work: file table with workspace/uploader indexes and size bytes.
- Security/permissions: membership-gated SAS URL generation; file type/size limits; no raw URL exposure.
- Tests required: upload validation, quota enforcement, cross-workspace denial, storage delete cleanup.
- Acceptance criteria: workspace members can upload/download files and see quota usage.
- Estimated complexity: M.
- Risk: Medium.
- Phase: Phase 3.

### 8. Advanced search across cards, docs, comments, and files

- Description: Expand `/api/search` and `SearchModal.razor` to include richer result types and ranking.
- Why users value it: The workspace becomes useful only when everything is findable.
- MVP scope: search cards/boards/docs/comments/filenames; filters by workspace and result type.
- What not to build yet: external search service, semantic search, AI answers.
- Backend work: expand `SearchResultType`, `SearchController`, indexes; maybe query service.
- Frontend work: grouped result UI, workspace filter, recent/favorites boost.
- Database work: text indexes/trigram extension decision for Postgres as data grows.
- Security/permissions: every result must be member-gated; snippets must not leak hidden content.
- Tests required: cross-workspace denial for every result type; ranking/scope tests.
- Acceptance criteria: Ctrl+K finds docs, comments, files, cards, and boards with clear navigation.
- Estimated complexity: M/L.
- Risk: Medium.
- Phase: Phase 3.

### 9. Favorites and recent items

- Description: Per-user favorites and recent boards/docs/cards surfaced in nav, dashboard, and command center.
- Why users value it: It improves daily navigation and makes large workspaces manageable.
- MVP scope: favorite board/doc; recent item tracking on open; nav/dashboard display.
- What not to build yet: folders, pins per team, shared navigation policies.
- Backend work: `FavoriteItem`, `RecentItem` entities/endpoints.
- Frontend work: star actions, dashboard sections, command palette recent results.
- Database work: user+workspace+target indexes; unique constraints.
- Security/permissions: favorites must be purged/hidden when membership is removed or target deleted.
- Tests required: cross-user isolation, removed membership hiding, deleted target cleanup.
- Acceptance criteria: favorites/recent survive sessions and never show inaccessible items.
- Estimated complexity: M.
- Risk: Low.
- Phase: Phase 2.

### 10. Command center actions

- Description: Expand Ctrl+K from search into actions: create card/board/doc, jump to dashboard, invite member, open settings.
- Why users value it: Power-user efficiency and premium polish.
- MVP scope: action result rows with keyboard execution and context-aware quick create.
- What not to build yet: command scripting, plugin commands, natural language commands.
- Backend work: minimal; use existing endpoints. Add quick create docs once docs exist.
- Frontend work: `SearchModal.razor` becomes command center with result/action groups.
- Database work: none.
- Security/permissions: only show actions allowed by current role/context.
- Tests required: component tests/Playwright for keyboard navigation and role-gated actions.
- Acceptance criteria: Ctrl+K can create a card/board/doc and route correctly without mouse use.
- Estimated complexity: M.
- Risk: Medium.
- Phase: Phase 2/3.

### 11. @mentions and card watchers

- Description: Mention users in comments/docs and let users watch cards for updates.
- Why users value it: Targeted collaboration beats noisy notifications.
- MVP scope: comment mentions, card watchers, notification/email preferences.
- What not to build yet: mentions for every object, threaded comments, reactions.
- Backend work: mention parsing service, `CardWatcher`, notification events, email templates.
- Frontend work: mention autocomplete in comment editor, watcher toggle, notification copy.
- Database work: watcher table; optional mention event table.
- Security/permissions: only mention workspace members; removed users stop receiving notifications.
- Tests required: mention parsing, notification recipients, preference respect, cross-workspace denial.
- Acceptance criteria: mentioning a member notifies them and watchers receive relevant card updates.
- Estimated complexity: M/L.
- Risk: Medium.
- Phase: Phase 2.

### 12. Workspace activity and audit trail expansion

- Description: Expand `ActivityEvent` into a workspace-wide history with event taxonomy and retention.
- Why users value it: Teams trust tools that explain what changed and who did it.
- MVP scope: events for member/invite/board/card/doc/file/template actions; workspace feed page.
- What not to build yet: immutable compliance export, SIEM integration, admin policy UI.
- Backend work: activity service wrapper, event names/constants, endpoints, retention config.
- Frontend work: workspace feed, board feed cleanup, dashboard recent activity.
- Database work: likely no new table; maybe add target title/snapshot fields to `ActivityEvent`.
- Security/permissions: member-gated; sensitive events need careful payloads.
- Tests required: events emitted; payloads do not contain secrets; cross-workspace denial.
- Acceptance criteria: a workspace member can see a coherent recent history across boards/docs/files.
- Estimated complexity: M.
- Risk: Medium.
- Phase: Phase 2.

### 13. Due date reminders

- Description: Background job creates notifications/email reminders before due dates.
- Why users value it: Reduces missed deadlines without complex automation.
- MVP scope: user preference for due reminders; daily/hourly job; one reminder per card/window.
- What not to build yet: arbitrary reminder schedules or calendar feed subscriptions.
- Backend work: reminder runner, notification creation, preference extension.
- Frontend work: preference controls and reminder copy.
- Database work: store reminder sent state or event idempotency key.
- Security/permissions: only notify assignees/watchers/workspace members.
- Tests required: idempotency, due window logic, preference respect, no reminders for archived/trashed cards.
- Acceptance criteria: due cards produce one timely notification and do not spam.
- Estimated complexity: M.
- Risk: Medium.
- Phase: Phase 2.

### 14. Recurring cards

- Description: Cards that regenerate on a schedule for weekly/monthly operations.
- Why users would value it: Real teams have repeated chores, reviews, invoices, reports, and maintenance.
- MVP scope: simple recurrence pattern on card template, next run, background creation.
- What not to build yet: complex RRULE UI, timezone-heavy calendar recurrence, exceptions/skip logic.
- Backend work: recurrence entity or fields, background runner, duplicate prevention.
- Frontend work: recurrence settings in card modal and template flows.
- Database work: recurrence metadata and last/next run indexes.
- Security/permissions: recurrence runs under workspace/system context; preserve audit actor semantics.
- Tests required: schedule creation, idempotency, archived/deleted behavior, permission assumptions.
- Acceptance criteria: a weekly task creates the next card once and logs the event.
- Estimated complexity: L.
- Risk: Medium.
- Phase: Phase 5.

### 15. Simple automation rules

- Description: Template-based rules such as "when card moves to Done, notify watchers" or "when priority becomes Critical, notify admins".
- Why users value it: Saves manual coordination without a complex workflow platform.
- MVP scope: fixed trigger/action templates, enable/disable, run history, plan limits.
- What not to build yet: arbitrary rule builder, webhooks, multi-step branching, third-party integrations.
- Backend work: `AutomationRule`, runner hooks in card update/move paths, history table.
- Frontend work: automation settings page with template cards and limits.
- Database work: rules and run history tables with workspace indexes.
- Security/permissions: Admin/Owner manage rules; actions must not bypass permissions or plan limits.
- Tests required: trigger/action execution, idempotency, plan limits, no cross-workspace side effects.
- Acceptance criteria: a workspace can enable a small set of reliable automations and inspect run history.
- Estimated complexity: L/XL.
- Risk: High.
- Phase: Phase 5.

## 7. Features that sound impressive but should not be built yet

| Feature | Decision | Reason |
| - | - | - |
| Full Notion block editor | Avoid | Editor complexity will swallow the product. Use simple rich text/Markdown. |
| AI assistant | Avoid | No clear core problem yet; likely to read as gimmick and add cost/privacy concerns. |
| Payment, billing, checkout, or subscription creation | Avoid now | Charging is not part of the current roadmap. Build the product first and decide at the end whether charging makes sense. |
| Complex Gantt | Delay/Avoid | High UI/data cost; weak fit for small-team lightweight positioning. |
| Full Jira sprint system | Avoid | Would reposition Planora into a bad Jira competitor. |
| Plugin marketplace | Avoid | Architecture burden with no user base. |
| Native mobile app | Avoid | Responsive web is the right scope now. |
| Complex analytics dashboard | Delay | Simple workspace/board summaries first. |
| Enterprise SSO/SAML | Delay | Business/enterprise need later; too early. |
| Advanced workflow builder | Delay/Avoid | Fixed automation templates first. |
| Public page publishing | Avoid | Permission/security/public-content surface is not aligned with team workspace focus. |
| Real-time cursor presence | Delay | Low value without collaborative docs and real-time editing. |
| Comments on every object | Avoid | Noise and maintenance. Comments on cards/docs are enough. |
| Full offline mode | Avoid | Very high sync/conflict complexity. |

## 8. Suggested phased roadmap

### Phase 1 - Make it product-safe

- Goal: Prepare the existing product for data ownership, observability, usage visibility, and continued expansion.
- Features:
  - central workspace authorization/role guard service
  - usage visibility for storage and object counts
  - workspace export
  - change email flow
  - terms/privacy/data-retention pages
  - health probe wiring validation in Azure Container Apps
  - error tracking/metrics baseline
  - permission test matrix for existing and new domains
- Rationale: The repo already has most account lifecycle/security foundations. The next safety work is expansion safety: every new docs/files/views/automation feature must reuse the same authorization, usage, and audit patterns.
- Implementation order:
  1. Workspace authorization helper.
  2. Usage counters for storage and object counts.
  3. Workspace export.
  4. Change email.
  5. Legal/retention pages.
  6. Observability/probe hardening.
  7. Tests.
- Risks: over-abstracting permissions; adding limits that hurt demo value; relying on counters that drift.
- Validation requirements: API integration tests for cross-workspace denial, usage safety, export scoping, and account changes.
- Do not include in this phase: Stripe, docs/wiki, automations, SignalR, table view.

### Phase 2 - Make it team-useful

- Goal: Make Planora feel like a daily team workspace, not only a board tool.
- Features:
  - workspace dashboard
  - favorites and recent items
  - server-backed saved views
  - workspace activity feed expansion
  - @mentions and card watchers
  - due date reminders
  - command center actions
  - onboarding checklist and template entry points
- Rationale: These features compound existing boards, comments, notifications, email, and activity.
- Implementation order:
  1. Dashboard summary endpoint/page.
  2. Favorites/recent model.
  3. Server saved views.
  4. Activity event service/taxonomy.
  5. Mentions/watchers.
  6. Reminder job.
  7. Command actions.
  8. Onboarding checklist.
- Risks: notification spam, event taxonomy drift, saved view schema migration.
- Validation requirements: role-gated action tests, notification recipient tests, dashboard scope tests, component/E2E tests for command center.
- Do not include in this phase: custom fields, docs editor, automations builder, payment/subscription implementation.

### Phase 3 - Make it workspace-complete

- Goal: Add the project knowledge layer and multiple ways to work with the same data.
- Features:
  - lightweight docs/wiki
  - board/card/doc templates
  - workspace files library
  - advanced search across docs/comments/files
  - list view
  - table view
  - custom fields MVP
  - improved calendar filters
- Rationale: This is where Planora becomes more than Kanban and earns the "workspace" positioning.
- Implementation order:
  1. Docs MVP.
  2. Docs search.
  3. Templates.
  4. Files library.
  5. List view.
  6. Table view.
  7. Custom fields.
  8. Calendar improvements.
- Risks: editor XSS, search leakage, custom-field complexity, oversized `Board.razor`.
- Validation requirements: sanitization tests, cross-workspace search tests, file quota tests, component/E2E coverage for views.
- Do not include in this phase: real-time collaborative editing, public pages, formulas, rollups.

### Phase 4 - Make it charge-optional

- Goal: Make the product mature enough that charging could be evaluated later, without implementing payments or subscriptions now.
- Features:
  - usage meters displayed in settings
  - storage safety limits
  - admin/account settings polish
  - workspace export improvements
  - support/help pages
  - clear packaging notes kept as documentation only
- Rationale: This phase should prove Planora is valuable and operable. It should not add a payment provider.
- Implementation order:
  1. Add settings usage UI.
  2. Add storage safety checks.
  3. Add workspace/account admin polish.
  4. Add support/legal/help pages.
  5. Document optional future packaging.
- Risks: accidentally building paywall architecture too early; demo friction; usage counters drifting.
- Validation requirements: usage tests, storage-limit tests, export tests, settings UI checks.
- Do not include in this phase: Stripe, checkout, customer portal, invoices, billing webhooks, payment methods, subscriptions, trials, coupons, tax, proration, or enterprise contracts.

### Phase 5 - Make it competitive

- Goal: Add selective power features once the workspace foundation is stable.
- Features:
  - SignalR board updates
  - recurring cards
  - simple automation rules
  - automation history and safety limits
  - advanced search ranking
  - CSV/Trello import
  - simple reporting summaries
  - polished onboarding/template library
- Rationale: These can differentiate Planora, but only after the core workspace has enough depth.
- Implementation order:
  1. Recurring cards.
  2. Fixed automation templates.
  3. Automation history/limits.
  4. SignalR board update notifications.
  5. Imports.
  6. Reporting summaries.
- Risks: background job idempotency, real-time auth/scaling, automation side effects.
- Validation requirements: job idempotency tests, real-time permission tests, import validation tests, load smoke tests.
- Do not include in this phase: plugin marketplace, public API marketplace, mobile app, full workflow builder.

## 9. Detailed implementation backlog

| # | Priority | Task | Goal | Complexity | Likely backend areas | Likely frontend areas | DB changes | Dependencies | Validation | Manual testing | Risk notes |
| - | - | - | - | - | - | - | - | - | - | - | - |
| 1 | P0 | Add workspace authorization helper | Centralize member/role checks before more domains are added | M | `Application/Services`, controllers | none | none | none | `dotnet test Planora.slnx` | existing board/workspace flows | Avoid changing behavior silently |
| 2 | P0 | Add workspace usage visibility service | Report storage/object counts for product safety, not billing | M | `Application/Interfaces`, `Program.cs`, options | settings copy later | `UsageCounters` optional | task 1 | unit/integration tests | local/demo mode | Must not break demo |
| 3 | P0 | Add storage safety checks | Prevent unbounded uploads without creating pricing plans | M | usage/storage service | settings warnings later | `UsageCounters` optional | task 2 | integration tests | upload/delete counters | Counter drift |
| 4 | P0 | Wire Azure health probes | Ensure Container Apps actually uses `/health/live` and `/health/ready` | S | deploy workflow/docs | none | none | none | workflow/config review | deploy smoke | Outward deployment action requires approval |
| 5 | P1 | Add workspace export | Export a workspace as JSON for owners/admins | M | `WorkspacesController`, account export mapper reuse | settings export button | none | task 1 | export scoping tests | download file | Data leak risk |
| 6 | P1 | Add change email flow | Complete account lifecycle | M | `AuthController`/`UsersController`, email service | `Profile.razor` | maybe none | email system | auth tests | change/confirm email | Token/account takeover risk |
| 7 | P1 | Add terms/privacy/data-retention pages | Paid launch trust basics | S | static config optional | new pages/nav/footer links | none | none | build | route/browser check | Keep legal text conservative |
| 8 | P1 | Add frontend component test foundation | Cover key Blazor UI behavior | M | test project refs | bUnit tests | none | none | new test command | n/a | Setup churn |
| 9 | P1 | Add Playwright smoke suite | Verify login/demo/board/card critical paths | M | test config | E2E project/scripts | none | dev server strategy | Playwright run | desktop/mobile smoke | Requires stable server orchestration |
| 10 | P1 | Expand ActivityEvent service/taxonomy | Stop hand-writing event strings as new domains arrive | M | `ActivityEvent`, service, controllers | activity rendering | maybe target title fields | task 1 | activity tests | board/member actions | Payload privacy |
| 11 | P1 | Workspace dashboard endpoint | Return due cards, recent boards, recent activity | M | `WorkspacesController` or dashboard service | none | none | task 10 | scope tests | API response | Query performance |
| 12 | P1 | Workspace dashboard UI | Make dashboard the workspace home | M | none | new page/section, nav | none | task 11 | bUnit/Playwright | mobile/desktop | Info overload |
| 13 | P1 | Favorites model | Persist favorite boards/docs later | M | new entity/controller | star controls | `FavoriteItems` | task 1 | isolation tests | favorite/unfavorite | Stale targets |
| 14 | P1 | Recent items tracking | Store recently opened boards/cards/docs | M | middleware/service/endpoints | nav/dashboard/search | `RecentItems` | task 13 optional | isolation tests | open items | Privacy/user isolation |
| 15 | P1 | Server-backed saved views entity | Persist board filter views | M | entity/config/controller | none | `SavedViews` | task 1 | CRUD/scope tests | API flow | JSON schema evolution |
| 16 | P1 | Replace local saved views UI with API | Share views across browsers/members | M | saved views API | `Board.razor` | none | task 15 | component/E2E | save/delete/apply | Preserve local UX |
| 17 | P1 | Command center action model | Add action rows to Ctrl+K | M | maybe none | `SearchModal.razor` | none | existing search | bUnit/Playwright | keyboard actions | Role-gated actions |
| 18 | P1 | Quick create board/card command | Create common objects from command center | M | existing endpoints | command modal flows | none | task 17 | E2E | create flows | Context selection |
| 19 | P1 | Workspace activity feed endpoint | Show events across boards/docs/files | S/M | `WorkspacesController` | none | none | task 10 | scope tests | API/feed | Retention assumptions |
| 20 | P1 | Workspace activity feed UI | Add feed to dashboard/settings | M | none | dashboard/feed component | none | task 19 | component/E2E | activity display | Noise |
| 21 | P1 | Mention parsing service | Detect `@user` references safely | M | service, comments controller | none | optional event refs | task 1 | parser tests | n/a | False positives |
| 22 | P1 | Mention autocomplete UI | Let users mention workspace members | M | members endpoint reuse | card comment editor | none | task 21 | component tests | keyboard/mobile | Accessibility |
| 23 | P1 | Mention notifications/emails | Notify mentioned users with preferences | M | notification/email services | notification copy | maybe none | tasks 21-22 | notification tests | mention flow | Spam |
| 24 | P1 | Card watchers model | Subscribe users to card updates | M | `CardWatcher` entity/controller | card modal watcher control | `CardWatchers` | task 1 | scope tests | watch/unwatch | Recipient rules |
| 25 | P1 | Due date reminder job | Notify before due dates | M | background runner, notifications | profile prefs | reminder state | notification prefs | job tests | reminder copy | Idempotency |
| 26 | P1 | Docs MVP domain | Add workspace/board documents | L | `Document` entity/controller/validators | none | `Documents` | task 1 | CRUD/scope tests | API | XSS/rendering |
| 27 | P1 | Docs editor/list UI | Users can write docs | L | docs API | docs pages/components | none | task 26 | bUnit/E2E | create/edit/search | Editor package choice |
| 28 | P1 | Docs search integration | Ctrl+K finds docs | M | `SearchController`, DTO enum | `SearchModal` groups | indexes maybe | task 26 | search scope tests | search docs | Snippet leaks |
| 29 | P1 | Board/card/doc template seed set | Add built-in templates | M | template seed/copy service | template picker | optional `Templates` | docs/cards stable | copy tests | create from template | Reference leaks |
| 30 | P1 | User-created workspace templates | Let teams save templates | M/L | `Template` entity/controller | settings/template UI | `Templates` | task 29 | permission tests | manage templates | Scope creep |
| 31 | P1 | Workspace files domain | General files beyond card attachments | M | file entity/controller/storage | none | `WorkspaceFiles` | usage metering | upload/scope tests | upload/delete | Quotas/security |
| 32 | P1 | Workspace files UI | Files page/panel | M | files API | files page/upload | none | task 31 | E2E | upload/download | Large-file UX |
| 33 | P1 | Advanced search result types | Search comments/files too | M | `SearchController` | `SearchModal` | indexes | tasks 26,31 | scope/ranking tests | search all types | Performance |
| 34 | P1 | List view MVP | Board cards in a list | M | maybe query endpoint | new board view component | none | filters stable | component/E2E | view switch | `Board.razor` size |
| 35 | P1/P2 | Table view MVP | Scan/edit card fields in table | L | card query/update reuse | table component | none | task 34 | conflict tests | edit cells | Complexity |
| 36 | P1/P2 | Custom field definitions | Define fields per board | L | entities/controllers/validators | field manager | `CustomFields` | task 35 | type tests | settings UI | DB design |
| 37 | P1/P2 | Custom field values | Store/render values on cards | L | value endpoints | card modal/table | `CardCustomFieldValues` | task 36 | scope/type tests | edit values | Migration/data shape |
| 38 | P2 | Board attachments | Add files linked to boards | M | reuse storage/file model | board settings/files | maybe generic attachments | workspace files | upload tests | upload/download | Duplication with files |
| 39 | P2 | Calendar filter improvements | Filter calendar by assignee/priority/board | M | calendar endpoint query | `Calendar.razor` | none | filters shared | tests | mobile calendar | Query complexity |
| 40 | P2 | Recurring cards MVP | Generate repeated tasks | L | recurrence entity/job | card/template settings | recurrence table | templates/jobs | job tests | recurrence flow | Idempotency |
| 41 | P2 | Automation rule templates | Fixed rule templates, not builder | L | `AutomationRule`, hooks | automation settings | rules/history | activity/notifications | trigger tests | enable/disable | Side effects |
| 42 | P2 | Automation history and safety limits | Debuggable automations without runaway jobs | M | run history, safety checks | history UI | `AutomationRuns` | task 41 | limit/idempotency tests | run history | Storage growth |
| 43 | P2 | Workspace usage UI | Show plan usage and limits | M | usage endpoints | settings panels | counters | tasks 2-3 | tests | quota display | Demo friction |
| 44 | P2/P3 | CSV import | Import cards/boards simply | M | import endpoint/validator | import modal | maybe import jobs | templates/views | validation tests | import file | Malformed data |
| 45 | P3 | SignalR board update notifications | Refresh board when others change it | L | hub/auth/events | board client handler | none | stable activity | hub tests | two-browser test | Scaling/auth |

## 10. Suggested first 10 implementation tasks

These are first because the obvious examples in the prompt - password reset, email verification, member lifecycle, sessions, attachments, Blob storage, upload rate limits, account export/deletion, 2FA, CI, and cleanup jobs - are already implemented in this repo.

### 1. Centralize workspace authorization/role checks

- Exact goal: add a service/helper used by controllers for member/role/resource ownership checks.
- Why early: every docs/files/views/custom-fields feature will need the same permission discipline.
- Acceptance criteria: existing behavior remains unchanged; new helper is used in one or two pilot controllers.
- Tests required: existing permission tests stay green; add helper-specific cross-workspace tests.
- Risks: accidental status-code changes.
- Do not include: broad controller rewrites.

### 2. Add workspace usage visibility service

- Exact goal: create a service that reports storage bytes and key object counts per workspace.
- Why early: docs/files/templates will add new storage and growth pressure; visibility prevents accidental runaway usage.
- Acceptance criteria: usage can answer questions like current storage bytes, active boards, attachments, docs, files, and automations.
- Tests required: upload/delete and create/delete scenarios keep usage accurate or recomputable.
- Risks: counter drift and over-engineering.
- Do not include: pricing plans, subscriptions, Stripe, checkout, billing portals, or webhooks.

### 3. Add storage safety checks

- Exact goal: prevent unbounded upload growth with configurable safety thresholds that apply equally in free/local/demo use.
- Why early: files and docs will increase storage pressure even if Planora never charges.
- Acceptance criteria: API can return usage summary and reject obviously unsafe upload growth with clear errors.
- Tests required: upload/delete updates storage usage; thresholds are scoped and configurable.
- Risks: counter drift.
- Do not include: billing UI, upgrade prompts, pricing tiers, or subscription logic.

### 4. Wire and verify Azure health probes

- Exact goal: ensure Container Apps uses `/health/live` and `/health/ready`.
- Why early: health endpoints exist but architecture docs still flag probe wiring.
- Acceptance criteria: deployment config references both probes and readiness fails closed when DB is down.
- Tests required: config review plus deployment smoke when approved.
- Risks: misconfigured probes can restart-loop production.
- Do not include: new observability stack.

### 5. Expand ActivityEvent through a service and taxonomy

- Exact goal: define event constants and a single service for writing activity events.
- Why early: dashboard, audit, docs, files, mentions, and automations all need consistent events.
- Acceptance criteria: card/board/member actions write standardized events.
- Tests required: event emission and no secret payload tests.
- Risks: noisy or inconsistent event payloads.
- Do not include: compliance audit export.

### 6. Build workspace dashboard endpoint

- Exact goal: return due soon, assigned to me, recent boards, and recent activity for one workspace.
- Why early: establishes the workspace OS direction with minimal new domain surface.
- Acceptance criteria: member-gated endpoint returns useful empty and populated states.
- Tests required: cross-workspace denial; archived/trashed filtering.
- Risks: slow queries as data grows.
- Do not include: customizable widgets.

### 7. Build workspace dashboard UI

- Exact goal: give each workspace a useful home view.
- Why early: visible product direction and activation value.
- Acceptance criteria: users can open a workspace and immediately see due work, activity, and next steps.
- Tests required: bUnit or Playwright coverage for empty/populated states.
- Risks: cluttered UI.
- Do not include: analytics dashboard.

### 8. Add favorites and recent items

- Exact goal: persist favorite and recent boards/cards/docs-ready targets per user.
- Why early: supports dashboard, search ranking, command center, and future docs.
- Acceptance criteria: favorites/recent survive sessions and hide inaccessible targets.
- Tests required: user isolation, membership removal, deleted target behavior.
- Risks: stale references.
- Do not include: shared team sidebar configuration.

### 9. Move saved board views to the server

- Exact goal: replace localStorage-only saved filters with API-backed saved views.
- Why early: current feature is useful but not team-grade.
- Acceptance criteria: saved views persist across browsers and can be shared within a workspace.
- Tests required: CRUD, invalid payload, cross-workspace denial.
- Risks: schema migration for filter JSON.
- Do not include: dashboards or cross-board reports.

### 10. Add bUnit/Playwright test foundation

- Exact goal: create frontend test coverage for command palette, workspace settings, dashboard, and board modal flows.
- Why early: frontend complexity will rise sharply with docs/views/files.
- Acceptance criteria: at least one component test and one E2E smoke path run locally/CI or documented separately.
- Tests required: the new tests themselves.
- Risks: fragile test setup around Blazor WASM/dev servers.
- Do not include: exhaustive visual regression suite.

## 11. Payment/subscription not-to-do strategy

### Current decision

Do not build payments or subscriptions now. The product roadmap should improve Planora first, then revisit whether charging is desirable after the product is clearly useful.

### Explicit not-to-do list

- Stripe
- payment methods
- checkout
- subscription creation
- subscription management
- customer portal
- invoices
- tax handling
- proration
- coupons
- trials
- dunning
- billing webhooks
- paid seats
- paid-plan downgrade behavior
- hard paywalls
- upgrade prompts
- billing admin screens
- fake subscription tables that will be thrown away later

### What is still useful without charging

- storage usage visibility
- file-size safety limits
- object-count summaries
- activity retention policy
- export job tracking
- admin/account settings polish
- legal/privacy/data-retention pages
- documentation of possible future packaging, kept separate from implementation

### What to track for product health, not billing

- active members
- active boards
- storage bytes
- largest files
- docs/pages
- saved views
- custom fields
- automation rules
- automation runs
- activity/audit retention
- export jobs

### Revisit criteria

Only reconsider payment/subscription work after Planora has:

- workspace dashboard
- server-backed shared views
- docs/wiki MVP
- workspace files with safe storage limits
- advanced search across docs/comments/files
- templates
- list/table view
- custom fields MVP
- mentions/watchers
- polished onboarding
- frontend smoke coverage
- clear evidence that charging would improve, not distort, the product

Even then, the next step should be a product decision, not an automatic Stripe implementation.

## 12. Database and domain model implications

| Concept | Status | Purpose | Relationships | Ownership/access rules | Indexes needed | Cascade/delete behavior | Migration risk |
| - | - | - | - | - | - | - | - |
| ActivityEvent | Exists, expand | Feed/audit backbone | User, Workspace, optional Board, target type/id | workspace members only; admin retention later | `(WorkspaceId, CreatedAt)`, `(BoardId, CreatedAt)`, target | cascade workspace; set null board | Low/Medium if adding columns |
| Attachment | Exists for cards, generalize | File metadata for cards/boards/workspaces | target + uploader + storage URL | workspace membership and upload limits | target id, workspace id if generalized | delete row must delete storage object | Medium |
| WorkspaceSettings | New | Store onboarding, defaults, retention, feature prefs | one-to-one Workspace | Owner/Admin edit | unique WorkspaceId | cascade with workspace | Low |
| NotificationPreference | Partial on AppUser | Control email/in-app notification topics | User, maybe workspace overrides | user owns personal prefs; workspace admins may set defaults later | UserId, optional WorkspaceId | cascade user/workspace | Medium if migrating booleans |
| WorkspacePlan or Subscription | Avoid now | Do not model payment plans or subscriptions until charging is explicitly chosen later | none now | none now | none now | none now | High if built prematurely |
| UsageCounter | New | Efficient limit checks | Workspace, period maybe | internal only; admins can view summary | WorkspaceId, metric, period | cascade workspace | Medium due drift |
| Document/Page | New | Lightweight docs/wiki | Workspace, optional Board, author/updater | workspace members read; edit by members initially | WorkspaceId+UpdatedAt, BoardId, title/search | soft delete; cascade workspace | Medium |
| Template | New | Reusable board/card/doc structure | Workspace optional; creator; type | built-ins global; workspace templates scoped | WorkspaceId+Type, CreatedBy | cascade workspace; copy not reference | Medium |
| CustomField | New | Board-specific metadata schema | Board, options, values | Admin/Owner manage definitions; members edit values | BoardId+Position, Type | cascade board; restrict option deletion carefully | High |
| SavedView | New | Shared filters/sorts/views | Workspace, Board, creator | private to creator or shared to members | BoardId+Name, CreatorId | cascade board/workspace | Medium |
| AutomationRule | New | Simple workflow rules | Workspace, creator, optional board scope | Owner/Admin manage | WorkspaceId+Enabled | cascade workspace; keep run history retention | High |
| ExportJob | New later | Async workspace export bundles | Workspace, requested by user | Owner/Admin request; requester downloads | WorkspaceId+CreatedAt, Status | cascade workspace; delete blob on expiry | Medium |
| AuditLog | Use ActivityEvent first | Formal compliance history later | Workspace/user/target | Owner/Admin/Business only | WorkspaceId+CreatedAt | retention by plan | High if separate from ActivityEvent |
| UserSession | RefreshToken exists | Session list/revoke | User | user owns sessions | UserId, ExpiresAt | delete with user or explicit cleanup | Low |
| FavoriteItem | New | User navigation | User, workspace, target | user-owned, target must be accessible | UserId+Target | delete/hide on target removal | Low |
| RecentItem | New | Recents/ranking | User, workspace, target | user-owned | UserId+UpdatedAt | delete/hide on target removal | Low |
| CardWatcher | New | Targeted notifications | Card, user | workspace member only | CardId, UserId unique | cascade card; remove on member removal | Medium |

## 13. Security and permission implications

Expansion increases the IDOR surface. Every new feature must keep workspace scope as the primary boundary.

### Security rules that every new feature must follow

- User identity always comes from `ClaimTypes.NameIdentifier`, never from request bodies.
- Every resource must resolve to a workspace before read/write.
- Every read must verify `WorkspaceMembers` membership.
- Every privileged write must verify role: Owner/Admin for workspace settings, templates, custom fields, automations, usage safety, member management.
- Feature-limit checks must happen server-side, not only in Blazor.
- Plan limits must be evaluated after authorization and before mutation.
- File reads must go through authorized API responses and signed URLs; do not expose raw private blob URLs.
- Uploads must validate size, extension, content type, and magic bytes where applicable.
- New searchable content must never leak snippets from inaccessible workspaces.
- Background jobs must be idempotent and must not act on archived/trashed/deleted objects unless intended.
- Invite tokens, reset tokens, verification tokens, refresh tokens, and provider secrets must never be logged.
- Activity/audit payloads must not store secrets, tokens, raw email bodies, or large document content.
- Account deletion/export must preserve workspace ownership rules.
- Data export must include only data the requester is allowed to administer or access.
- Soft-delete and retention rules must be consistent across list/search/calendar/export.
- Every new endpoint gets permission/IDOR integration tests.

### Specific risks by expansion area

- Docs: XSS and unsafe rendering are the main risks.
- Files: malware-ish uploads, content sniffing, quota bypass, orphaned blobs, signed URL lifetime.
- Search: snippet leakage and cross-workspace result leakage.
- Custom fields: type validation and query performance.
- Automations: permission bypass through background actions.
- Payment/subscription implementation: intentionally out of scope; if reconsidered later, webhook replay, downgrade behavior, plan bypass, and local/dev coupling would be high-risk areas.
- Real-time: hub authentication, group membership, reconnect state, and cross-workspace broadcasts.

## 14. Testing strategy

### Keep building on the existing strengths

`Planora.Tests` already covers many API/security flows with a real API host and PostgreSQL. Keep that as the primary safety net for backend expansion.

### Add first

- Permission/IDOR tests for every new domain: docs, files, saved views, templates, custom fields, automations, workspace export.
- Usage safety tests: create board/doc/file/custom field at configured safety thresholds and above them.
- Search leakage tests for every result type.
- File upload/quota/delete cleanup tests.
- Activity event emission tests through a shared service.
- Background job idempotency tests for reminders/recurrence/cleanup/export.
- bUnit tests for reusable Blazor components once setup exists.
- Playwright E2E smoke tests for demo/login, workspace dashboard, board/card modal, docs, files, and command center.

### Add later

- Accessibility checks in Playwright or axe tooling.
- Visual regression for dashboard/docs/table view if UI churn becomes high.
- Load/performance smoke tests for search and dashboard queries.
- Payment and subscription tests are intentionally not needed now because payment/subscription implementation is not on the roadmap.
- SignalR multi-client tests if real-time updates are built.

### Test priorities

1. Security/permission tests.
2. Plan-limit and usage tests.
3. File/search/docs safety tests.
4. Background job idempotency tests.
5. Frontend component/E2E coverage for high-use flows.
6. Accessibility and visual regression.

## 15. Product quality definition

Planora is complete enough to consider broader real-world use only when:

- Onboarding: a new user can create or demo a workspace, choose a template, invite teammates, and understand next steps.
- Account lifecycle: register, login, 2FA, sessions, password reset, email verification, change email, export, and delete account all work.
- Team management: owners can manage members, roles, invites, ownership, workspace settings, and exports.
- Reliability: health probes, CI, monitoring, error tracking, cleanup jobs, and rollback notes exist.
- Security: permission tests cover every workspace-scoped domain; upload/search/export flows are leak-tested.
- UX polish: dashboard, boards, docs, files, search, profile, settings, and empty/error states feel coherent on desktop and mobile.
- Payment/subscription stance: no payment or subscription implementation exists unless a later explicit product decision changes that.
- Supportability: logs have correlation IDs, user-facing errors are understandable, and admin/support workflows are documented.
- Data ownership: account and workspace exports exist; retention rules are documented.
- Documentation: README, API endpoints, architecture, and product roadmap stay current.
- Tests: backend integration tests, frontend component/E2E smoke tests, and CI are green.

## 16. Final recommendation

Planora should become a focused small-team workspace OS: tasks, boards, lightweight docs, files, shared views, search, templates, and simple workflow support.

Build first:

1. central workspace authorization helper
2. workspace usage visibility
3. storage safety checks
4. workspace dashboard
5. expanded activity taxonomy/feed
6. favorites/recent items
7. server-backed saved views
8. command center actions
9. mentions/watchers
10. frontend test foundation

Delay:

- custom fields until list/table foundations exist
- automations until activity, notifications, and safety limits are stable
- payment/subscription implementation until an explicit future decision says charging is worth it
- SignalR until the app has enough collaborative surfaces to justify real-time infrastructure

Avoid:

- full Notion editor
- Jira sprint system
- plugin marketplace
- native mobile app
- AI assistant as a headline product
- public page publishing
- enterprise SSO before real business demand

What most improves Planora as a portfolio piece:

- server-backed shared views
- workspace dashboard
- lightweight docs with safe search
- workspace files with quota/security
- bUnit/Playwright coverage
- clean usage visibility and storage safety architecture before any future charging discussion

What most improves Planora as a real product that could later support charging:

- docs/wiki
- shared saved views
- advanced search
- templates
- workspace files and storage quotas
- custom fields with table view
- mentions/watchers
- simple automations
- workspace export and longer activity retention

Minimum state before reconsidering whether to charge:

- usage visibility
- storage safety limits
- workspace dashboard
- server-backed shared views
- docs MVP
- workspace files/quota
- advanced search
- templates
- at least one advanced view/custom-field capability
- legal/privacy pages
- tests proving usage safety and permissions cannot be bypassed

The uncomfortable answer: payment and subscriptions are not a milestone right now. The next milestone is making Planora feel like the place a small team starts its day.
