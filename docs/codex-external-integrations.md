# Codex external integration policy and runbook

Last audited: 2026-07-15

Owner: Planora repository owner. Security-sensitive permission changes require review by the
`security-auditor`; deployment access changes also require the `azure-deployment-reviewer`.

## Decision

Planora needs a small read-oriented integration layer, not a general-purpose operator account.
This audit adds only the public OpenAI Developer Docs and Microsoft Learn MCP servers to
`.codex/config.toml`. They are first-party, read-only documentation services and require no
credential.

The installed OpenAI GitHub plugin, OpenAI Browser plugin, and Google Chrome team's Chrome
DevTools MCP already cover repository collaboration and interactive browser diagnosis. GitHub
and Chrome DevTools are usable on this workstation, but GitHub's current identity has write
access and therefore is not an acceptable unattended least-privilege identity. Azure CLI,
`psql`, and Docker are not installed. No Azure, Resend, PostgreSQL, or production credential was
created or requested by this change.

The repository has no staging environment definition, infrastructure-as-code, browser/E2E test
suite, persistent error tracker, Application Insights setup, or checked-in production log-sink
configuration. Those are gaps in the current system, not invitations to infer access or invent
configuration.

## Existing repository automation

| Area | Current state | External-system gap |
|---|---|---|
| Governance | `AGENTS.md`, 32 Skills, 36 Subagents, and the non-recursive subagent protocol | None; these are local instructions, not credentials |
| Hooks | `.codex/hooks.json` and `.codex/hooks/planora_hooks.py` enforce policy and run local quality gates | No GitHub, Azure, browser, email, or database API access |
| CI | GitHub Actions restores, builds, tests against PostgreSQL 16, formats, and validates Skills/Subagents/hooks | Logs and reruns require GitHub access |
| Deployment | GitHub Actions deploys the API to Azure Container Apps and the Web app to Azure Static Web Apps | Workflows can mutate production and must never be exposed as an autonomous deploy path |
| Storage | Private Azure Blob storage with service-generated SAS reads | Live inspection requires a separate data-plane role; account keys are prohibited |
| Tests | xUnit integration tests use a throwaway local `planora_test` PostgreSQL database | No Playwright/bUnit/browser E2E suite; browser checks are manual |
| Observability | JSON console logs, correlation response header, `/health/live`, and DB-backed `/health/ready` | No repository-configured log backend, trace backend, alerting, client error tracking, or verified Container Apps probes |
| Email | Resend in production; console/fake sender locally and in tests | Resend exposes no read-only API-key permission for delivery-log access |

## Integration inventory

`Required now` means autonomous diagnosis is materially blocked without the capability. It does
not authorize setup or grant permission. `Useful later` means wait for the named prerequisite or
a concrete incident.

| Capability | Classification | Minimum mechanism | Status and boundary |
|---|---|---|---|
| GitHub issues, PRs, reviews, and Actions | **Required now** | Existing OpenAI-curated GitHub plugin for interactive work; official GitHub MCP remote server in read-only mode for unattended reads | Plugin and `gh` are installed, but the current account/token has repository write and broad `repo` scope. Until a read-only profile is configured, use reads only and require a human confirmation for every write, rerun, review submission, branch push, or PR mutation. |
| Browser automation | **Required now** | Installed OpenAI Browser plugin | Use an isolated test session. Never expose a personal or production-admin session to autonomous browser control. |
| Chrome console/network/performance inspection | **Required now** | Official `chrome-devtools-mcp` | Already configured globally and health-checked with `list_pages`. Disable usage statistics if repository policy requires it. Do not attach to a browser containing secrets or unrelated authenticated tabs. |
| Current official OpenAI documentation | **Required now** | OpenAI Developer Docs MCP | Added locally; public, read-only, no credential. |
| Current official Microsoft/.NET/Azure documentation | **Required now** | Microsoft Learn MCP | Added locally; public, read-only, no credential. |
| Azure Container Apps resource state and logs | **Required now** | Azure CLI using an Entra identity with Reader plus narrowly scoped monitoring access | Manual authorization required. No deploy, secret-list, registry, or write permission. The official Azure MCP server is deferred because its mixed read/write catalog adds no necessary capability over the CLI for this repository today. |
| Azure Static Web Apps metadata | **Required now** | Azure CLI read commands plus GitHub Actions read access | Manual authorization required. Deployment-token access and deployment commands are prohibited. Application diagnostics still require an approved telemetry backend. |
| Production and staging logs | **Required now** | Azure CLI against Container Apps and the environment's Log Analytics workspace | Manual authorization required. Production and staging identities/scopes must be separate. A staging resource is not currently defined in the repository. Log access must exclude secrets and sensitive payloads. |
| Azure Blob object inspection | **Useful later** | Entra identity with `Storage Blob Data Reader`, scoped to the specific non-production container where possible | Keep outside the base Azure profile. Do not grant Shared Key, connection-string, SAS-minting, `Storage Blob Data Contributor`, or `Reader and Data Access`. Production blob contents require an incident-specific approval. |
| Azure MCP server | **Useful later** | Microsoft's official `@azure/mcp` with an exact tool allowlist and read-only Entra identity | Reconsider only after Azure CLI read access is proven insufficient. Do not expose write-capable tools merely for convenience. |
| Error tracking | **Useful later** | Azure Monitor/Application Insights using approved OpenTelemetry instrumentation | No tracker exists. Adding SDKs, telemetry, sampling, retention, or client reporting changes product code and privacy posture, so it requires a separate reviewed task. Do not add Sentry or another vendor speculatively. |
| PostgreSQL development inspection | **Required now** | Official `psql` client and a local-only service entry | `psql` is absent. Install the official client deliberately; use only the throwaway development/test database. EF/test tooling remains sufficient for normal tests. |
| PostgreSQL staging inspection | **Useful later** | `psql` with a database-enforced read-only role and TLS | Configure only after a real staging database exists. Production DB access is not a substitute. |
| Email-provider delivery logs | **Unsafe or excessive** | None at present | Resend API keys are only `sending_access` or `full_access`; log reads require the excessive full-access class. Do not configure Resend MCP or give Codex the production send key. Use sanitized application logs or a human-supervised dashboard until Resend offers a read-only scope. |
| Direct production PostgreSQL access | **Unsafe or excessive** | None | Never grant Codex a production database login, including read-only, as a default integration. Use approved telemetry and incident exports instead. No production write access is permitted under any circumstance. |
| Azure Contributor, deployment, secret, registry, or account-key access | **Unsafe or excessive** | None | Never permit automatic production deployment or access to deployment credentials, Container App secrets, SWA deployment tokens, registry credentials, storage keys, or connection strings. |
| Separate Playwright MCP or generic browser agent | **Unnecessary** | Existing Browser plugin and Chrome DevTools MCP | It would duplicate interactive capabilities. A repeatable repository E2E suite may be proposed separately, but that is testing infrastructure, not an external integration. |
| Generic third-party documentation/search MCP | **Unnecessary** | First-party docs MCPs and normal web research | Prefer the current official publisher's documentation and primary sources. |
| Repository plugin packaging | **Unnecessary** | Keep `.agents/`, `.codex/agents/`, and hooks repository-local | There is no demonstrated cross-repository reuse, independent release cadence, or distribution audience yet. Package only when one of those benefits becomes concrete. |

## Configuration locations and credentials

| Integration | Configuration | Credential location | Permitted environment variables |
|---|---|---|---|
| Public docs MCPs | Repository `.codex/config.toml` | None | None |
| Browser and Chrome DevTools | User Codex/plugin configuration outside the repository | Browser profile outside repository | None required; add `--no-usage-statistics` to the user-level Chrome DevTools MCP arguments if desired |
| GitHub read-only MCP | User-level Codex config, not the repository file | OS credential manager or session/user environment outside repository | `PLANORA_GITHUB_READ_TOKEN` only; never place its value in `.env`, TOML, workflow files, or shell history |
| GitHub CLI | User-level `gh` credential store | `gh auth login` secure store | Do not export `GH_TOKEN` persistently for this repository |
| Azure read-only | Azure CLI user cache outside repository | Entra sign-in; separate staging and production identities or accounts | Optional non-secret selectors `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`; do not store client secrets |
| Blob opt-in | Same Entra mechanism, separate narrowly scoped identity | Entra sign-in outside repository | Non-secret account/container selectors only |
| PostgreSQL dev/staging | `%APPDATA%\postgresql\.pg_service.conf` | `%APPDATA%\postgresql\pgpass.conf` or OS secret manager | `PGSERVICE=planora-dev-readonly` or `planora-staging-readonly`; optional `PGSERVICEFILE` outside repository |
| Resend | No Codex integration | Existing production secret store remains inaccessible | Never expose `RESEND_API_KEY` to Codex |

The repository ignores `.env` and `appsettings.Development.json`, but ignored files are not an
approved long-term credential store. Never copy a credential into prompts, task transcripts,
logs, documentation, hook receipts, test output, or Git configuration.

## Manual authorization procedures

### GitHub: establish an enforced read-only path

Official mechanism: the [GitHub MCP server](https://github.com/github/github-mcp-server), using
its [remote-server setup](https://github.com/github/github-mcp-server/blob/main/docs/remote-server.md).
Keep the OpenAI-curated GitHub plugin for its Codex workflows, but do not treat its write-capable
interactive identity as the unattended profile.

1. In GitHub, create a fine-grained personal access token limited to `Caximorris/Planora`.
2. Grant repository **Metadata: read**, **Contents: read**, **Issues: read**, **Pull requests:
   read**, **Actions: read**, and **Checks: read** if the token UI exposes Checks separately.
   Grant no administration, environments, deployments, secrets, workflows, or contents write.
3. Store the token as the user/session environment variable `PLANORA_GITHUB_READ_TOKEN` outside
   the repository.
4. Add this user-level Codex configuration, not a repository credential:

   ```toml
   [mcp_servers.planoraGitHubReadOnly]
   url = "https://api.githubcopilot.com/mcp/"
   bearer_token_env_var = "PLANORA_GITHUB_READ_TOKEN"
   http_headers = { "X-MCP-Toolsets" = "repos,issues,pull_requests,actions", "X-MCP-Readonly" = "true" }
   ```

5. Restart Codex and inspect `/mcp`. List the repository, one PR, one issue query, and recent
   Actions runs. Confirm that creating an issue, commenting, approving, rerunning a job, or
   changing a file is unavailable.
6. Run `gh auth status` separately. The current `gh` identity remains interactive and must not
   be used as proof that the MCP profile is read-only.

Codex gains issue/PR/review context and Actions status/log diagnosis. The residual risk is
source-code and operational-metadata disclosure plus token theft. Repository scoping,
read-only headers, read-only fine-grained permissions, short expiry, and revocation reduce that
risk. Review GitHub's current
[fine-grained token permission reference](https://docs.github.com/en/rest/authentication/permissions-required-for-fine-grained-personal-access-tokens)
before renewal.

### Azure: create separate read-only operational identities

Official mechanisms: [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/) now; Microsoft's
[Azure MCP server](https://learn.microsoft.com/en-us/azure/developer/azure-mcp-server/get-started/tools/visual-studio)
only if a later need justifies it.

1. An Azure administrator creates separate Planora staging and production identities/groups.
2. At the smallest applicable resource-group or resource scope, assign Azure **Reader** for
   Container Apps and Static Web Apps metadata. Do not assign Contributor.
3. At the exact Log Analytics workspace, assign **Log Analytics Reader** or a narrower custom
   role limited to the required tables. Add **Monitoring Reader** only if metrics/alerts require
   it. Do not grant workspace configuration writes.
4. Do not grant `listSecrets`, registry push/pull credentials, SWA deployment-token access,
   Key Vault secret access, Container Apps write actions, or deployment permissions.
5. Install the official Azure CLI, run `az login --tenant <tenant-id>`, then explicitly select
   the non-production subscription first with `az account set --subscription <id>`.
6. Verify identity and read access:

   ```powershell
   az account show --query "{tenant:tenantId,subscription:id,user:user.name}"
   az containerapp show --name planora-api --resource-group <resource-group>
   az staticwebapp list --resource-group <resource-group> --output table
   az containerapp logs show --name planora-api --resource-group <resource-group> --type console --tail 20
   ```

7. Verify denial: `az containerapp update`, secret listing, deployment-token reset, and role
   assignment operations must be unavailable. Do not actually mutate a resource merely to test
   denial; use `az role assignment list` and Azure access reviews to inspect effective grants.

Codex gains resource state, revisions, health context, and sanitized runtime logs. The risks are
production metadata/PII exposure and accidental mutation if roles are too broad. Separate
identities, resource scoping, read-only RBAC, short sign-in sessions, and log hygiene are
mandatory. Refer to the official [Azure built-in roles](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles),
[Log Analytics access model](https://learn.microsoft.com/en-us/azure/azure-monitor/logs/manage-access),
and [Container Apps log commands](https://learn.microsoft.com/en-us/azure/container-apps/log-streaming).

### Blob Storage: optional non-production data-plane read

1. An Azure administrator grants **Storage Blob Data Reader** on the exact non-production
   storage account or container. Keep it off the normal Azure operational identity when
   practical.
2. Authenticate with Entra ID; never provide an account key, connection string, or reusable SAS.
3. Verify with:

   ```powershell
   az storage blob list --account-name <account> --container-name <container> --auth-mode login --num-results 5
   ```

4. Confirm upload, overwrite, delete, SAS generation, and key listing are unavailable.

Codex gains object names and, only when explicitly approved, content inspection for staging
upload incidents. Object names and contents may contain personal data, so production access is
incident-specific. See Microsoft's [Entra authorization for blobs](https://learn.microsoft.com/en-us/azure/storage/blobs/authorize-access-azure-active-directory).

### PostgreSQL: local and future staging inspection

Install only the official PostgreSQL client. A database administrator, not Codex, creates the
staging login and executes grants. The enforceable boundary is `CONNECT`/`USAGE`/`SELECT`; a
read-only transaction default is defense in depth.

```sql
CREATE ROLE planora_codex_staging LOGIN PASSWORD '<generated-outside-repository>';
GRANT CONNECT ON DATABASE <staging_database> TO planora_codex_staging;
GRANT USAGE ON SCHEMA public TO planora_codex_staging;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO planora_codex_staging;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT ON TABLES TO planora_codex_staging;
ALTER ROLE planora_codex_staging SET default_transaction_read_only = on;
ALTER ROLE planora_codex_staging SET statement_timeout = '30s';
```

Store host, port, database, user, `sslmode=require`, and a non-secret service name in
`%APPDATA%\postgresql\.pg_service.conf`; store the password in
`%APPDATA%\postgresql\pgpass.conf`. Use separate `planora-dev-readonly` and
`planora-staging-readonly` services. Verify:

```powershell
$env:PGSERVICE = "planora-staging-readonly"
psql -X -v ON_ERROR_STOP=1 -c "select current_user, current_database(); show transaction_read_only; show statement_timeout;"
psql -X -v ON_ERROR_STOP=1 -c "select schemaname, tablename from pg_catalog.pg_tables where schemaname = 'public' limit 10;"
```

The login must have no table writes, DDL, role creation, replication, database creation,
extension management, bypass-RLS, or ownership. Codex gains schema/query-plan and sanitized row
inspection in development or staging. The risk is sensitive-row disclosure and load from bad
queries; narrow grants, TLS, timeouts, row limits, and a staging-only dataset are required. See
PostgreSQL's [connection service file](https://www.postgresql.org/docs/current/libpq-pgservice.html),
[password file](https://www.postgresql.org/docs/current/libpq-pgpass.html), and
[read-only transaction setting](https://www.postgresql.org/docs/current/runtime-config-client.html).

### Email logs and error tracking

No authorization procedure is approved today. Resend's official
[MCP server](https://resend.com/docs/mcp-server) can operate the account, while its
[API-key permissions](https://resend.com/docs/dashboard/api-keys/introduction) do not offer a
read-only log role. A human may inspect a sanitized dashboard during an incident; Codex must not
receive a full-access or production sending credential.

Application Insights/OpenTelemetry is a future product and privacy decision, not merely an MCP
connection. If approved later, instrument staging first, define redaction/sampling/retention,
keep the connection string in the deployment secret store, and expose only Azure Monitor
read access to Codex. Use Microsoft's
[ASP.NET Core OpenTelemetry guidance](https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-configuration)
as the baseline.

## Health checks

Run these without printing credential values:

| Tool | Health check | Expected result |
|---|---|---|
| Codex MCP configuration | `codex mcp list` or `/mcp` after restarting Codex | `openaiDeveloperDocs` and `microsoftLearn` are enabled; both answer a documentation query |
| GitHub interactive tooling | `gh auth status`; `gh repo view Caximorris/Planora`; `gh run list -R Caximorris/Planora --limit 5` | Identity is explicit and reads succeed; do not infer least privilege from success |
| GitHub read-only MCP | `/mcp`, then list Planora PRs and Actions runs | Reads succeed and write/rerun tools are absent |
| Browser plugin | Open the local app in an isolated test tab and inspect visible state | No unrelated authenticated tabs are shared |
| Chrome DevTools MCP | Invoke `list_pages`, then inspect console/network on the test tab | Test tab is visible and diagnostic reads succeed |
| Azure | `az account show`; resource `show/list`; a 20-line log tail | Correct tenant/subscription/identity; no write grants |
| Blob opt-in | `az storage blob list ... --auth-mode login --num-results 5` | Narrow container read succeeds; writes and key listing remain denied |
| PostgreSQL | `psql service=<name> -X -c "show transaction_read_only"` | `on`; correct user/database; bounded SELECT works |
| Resend | None | No Codex credential or MCP connection exists |

## Troubleshooting

- If a new repository MCP does not appear, restart Codex, verify the repository is trusted, and
  parse `.codex/config.toml` as TOML. Do not move secrets into the repository to debug it.
- If a docs server fails, open its first-party endpoint in a normal network context and check
  proxy/TLS policy. Do not substitute an unreviewed mirror.
- If GitHub reads return 403, inspect token expiry, repository selection, and individual
  fine-grained permissions. Do not broaden to classic `repo` as a shortcut.
- If an Actions log is unavailable, confirm **Actions: read**, that the run has retained logs,
  and that the repository is within token scope.
- If Chrome DevTools attaches to the wrong browser, stop it and use a clean test profile. Never
  continue in a profile containing production admin or personal sessions.
- If Azure returns authorization errors, confirm tenant, subscription, resource scope, and role
  propagation. Do not add Contributor or expose workflow deployment credentials.
- If Container Apps logs are empty, check whether console/system logs are emitted and whether
  the environment has a Log Analytics destination. Absence of a repository setting does not
  prove the live environment has none.
- If `psql` cannot connect, verify the service file, TLS, network allowlist, port, and exact
  staging role. Do not fall back to an application owner or production connection string.
- If a tool requests a production secret, stop. The integration design is wrong or the task
  needs an explicit human-run incident procedure.

## Skill-to-tool map

Tool profiles used below: **GH-R** GitHub read-only; **Browser** isolated OpenAI Browser plugin;
**DevTools** Chrome DevTools MCP; **Azure-R** Azure control-plane/log reads; **Blob-R** opt-in
non-production blob reads; **PG-Dev/PG-Stg** bounded PostgreSQL reads; **MS-Docs/OAI-Docs** the
first-party documentation MCPs. `Local only` means repository, Git, .NET, and existing local
test tooling. No profile permits external writes or production deployment.

| Skill | Allowed external tools |
|---|---|
| `planora-workflow` | Local only; MS-Docs/OAI-Docs when current platform rules matter |
| `investigate-bug` | Browser, DevTools; GH-R, Azure-R, PG-Stg only when the incident requires them |
| `investigate-performance` | DevTools, Azure-R, PG-Dev/PG-Stg |
| `investigate-ci` | GH-R; PG-Dev for local reproduction |
| `implement-endpoint` | MS-Docs, PG-Dev; no live-system writes |
| `implement-blazor-feature` | Browser, DevTools, MS-Docs |
| `change-api-contract` | GH-R for consumer context; MS-Docs |
| `optimize-performance` | DevTools, PG-Dev; Azure-R only for an established baseline |
| `add-tests` | PG-Dev; Browser/DevTools only for a manual complement |
| `run-integration-tests` | PG-Dev only |
| `verify-regression` | Browser, DevTools, PG-Dev; GH-R for the originating report |
| `review-code` | GH-R; other tools only when the diff touches that system |
| `review-architecture` | MS-Docs/OAI-Docs; GH-R for design context |
| `review-tech-debt` | GH-R for recurring evidence; diagnostic reads only |
| `review-release` | GH-R, Browser, DevTools, Azure-R, PG-Stg; all read-only and never deploy |
| `review-api-contract` | GH-R, MS-Docs; Browser/DevTools for Web consumers |
| `review-authz` | Browser, DevTools, PG-Dev; never production identity/session access |
| `audit-security` | GH-R, Browser/DevTools in isolation, Azure-R, optional Blob-R; never secrets |
| `review-ef-core` | PG-Dev; PG-Stg schema/query-plan reads only when authorized |
| `review-migration` | PG-Dev; PG-Stg metadata reads, never apply |
| `review-dependencies` | GH-R and first-party MS-Docs/OAI-Docs; no package publishing |
| `review-blazor-ui` | Browser, DevTools |
| `audit-responsive-ui` | Browser, DevTools |
| `audit-accessibility` | Browser, DevTools |
| `review-ux-visuals` | Browser, DevTools |
| `review-observability` | Azure-R, GH-R; PG health reads; no Resend MCP |
| `review-error-handling` | Browser, DevTools, Azure-R |
| `review-azure-deployment` | GH-R, Azure-R, MS-Docs; never workflow dispatch or Azure writes |
| `review-container` | Local only; MS-Docs for runtime specifications |
| `review-blob-storage` | Azure-R metadata and opt-in Blob-R; never keys/connection strings |
| `review-email-workflow` | Azure-R sanitized application logs; local fake sender; no Resend MCP |
| `review-notifications` | Browser, DevTools, PG-Dev/PG-Stg reads |

## Subagent-to-tool map

Subagents remain subject to `.codex/agents/PROTOCOL.md`: reviewers are read-only, delegation is
non-recursive, and no subagent may deploy or mutate production.

| Subagent | Allowed external tools |
|---|---|
| `accessibility-reviewer` | Browser, DevTools |
| `api-contract-reviewer` | GH-R, MS-Docs, Browser/DevTools |
| `api-engineer` | MS-Docs, PG-Dev |
| `architecture-reviewer` | GH-R, MS-Docs/OAI-Docs |
| `auth-session-specialist` | Browser, DevTools, PG-Dev; no production sessions |
| `authorization-reviewer` | Browser, DevTools, PG-Dev; no production sessions |
| `azure-deployment-reviewer` | GH-R, Azure-R, MS-Docs; never deploy |
| `backend-performance-reviewer` | Azure-R, PG-Dev/PG-Stg |
| `blazor-engineer` | Browser, DevTools, MS-Docs |
| `blazor-ui-reviewer` | Browser, DevTools |
| `blob-storage-reviewer` | Azure-R metadata, opt-in Blob-R |
| `bug-investigator` | GH-R, Browser, DevTools, Azure-R/PG-Stg when incident-scoped |
| `ci-investigator` | GH-R, PG-Dev for reproduction |
| `code-reviewer` | GH-R; system-specific reads only when relevant |
| `container-reviewer` | Local only; MS-Docs |
| `documentation-reviewer` | GH-R, MS-Docs/OAI-Docs |
| `domain-logic-engineer` | PG-Dev; MS-Docs when framework behavior matters |
| `ef-core-specialist` | PG-Dev, PG-Stg schema/query-plan reads |
| `email-workflow-reviewer` | Azure-R sanitized logs; local fake sender; no Resend MCP |
| `feature-reviewer` | GH-R, Browser, DevTools, PG-Dev |
| `frontend-performance-reviewer` | Browser, DevTools |
| `integration-test-engineer` | PG-Dev only |
| `migration-reviewer` | PG-Dev, PG-Stg metadata reads; never apply |
| `motion-reviewer` | Browser, DevTools |
| `notification-reviewer` | Browser, DevTools, PG-Dev/PG-Stg reads |
| `observability-reviewer` | Azure-R, GH-R, PG health reads |
| `refactoring-specialist` | GH-R for scope/history; otherwise local only |
| `regression-verifier` | GH-R, Browser, DevTools, PG-Dev |
| `release-reviewer` | GH-R, Browser, DevTools, Azure-R, PG-Stg; never deploy |
| `responsive-reviewer` | Browser, DevTools |
| `secret-leak-reviewer` | GH-R metadata/diff only; never retrieve or validate live secrets |
| `security-auditor` | GH-R, isolated Browser/DevTools, Azure-R, optional Blob-R; never secrets |
| `supply-chain-reviewer` | GH-R, first-party documentation; no package publishing |
| `tech-debt-reviewer` | GH-R for recurring evidence; otherwise local only |
| `test-engineer` | PG-Dev; Browser/DevTools for manual validation only |
| `ux-design-reviewer` | Browser, DevTools |

## Update policy

- Re-audit this document quarterly and whenever a provider, hosting architecture, staging model,
  Codex plugin, MCP server, workflow permission, or telemetry backend changes.
- Verify server names, endpoints, tool annotations, authentication, and permission semantics
  against current first-party documentation before enabling or renewing an integration.
- Keep repository MCP configuration credential-free. User-level credentials must be revocable,
  short-lived where possible, individually attributable, and scoped to one environment.
- Review effective GitHub and Azure permissions at least quarterly; revoke unused identities and
  expired incident access immediately.
- Any new write-capable external tool, production data access, email-provider access, database
  access, deployment capability, or secret access requires explicit human approval and a threat
  review. It is never inherited from this document.
- Do not package repository Skills, Subagents, or hooks as a plugin until at least two repositories
  need the same independently versioned bundle or there is a concrete distribution owner and
  support policy.

## First-party references

- [Codex MCP configuration](https://learn.chatgpt.com/docs/extend/mcp.md) and
  [project configuration](https://learn.chatgpt.com/docs/config-file/config-basic.md)
- [OpenAI Developer Docs MCP](https://developers.openai.com/learn/docs-mcp)
- [Microsoft Learn MCP](https://learn.microsoft.com/en-us/training/support/mcp)
- [Chrome DevTools MCP](https://github.com/ChromeDevTools/chrome-devtools-mcp)
- [GitHub MCP server](https://github.com/github/github-mcp-server)
- [Azure MCP tools](https://learn.microsoft.com/en-us/azure/developer/azure-mcp-server/tools/)
- [Azure Static Web Apps CLI](https://learn.microsoft.com/en-us/cli/azure/staticwebapp?view=azure-cli-latest)
