# Verification matrix

Select the narrowest checks that can disprove the change, then expand according to blast radius. Never build while Planora dev servers are live.

| Change or task | Minimum focused evidence | Expansion gate |
|---|---|---|
| Documentation or skills only | Skill validators, diff, `git diff --check`, status | No .NET build unless commands/config were changed |
| API implementation | Affected project build and focused integration tests | Full solution build/test for Shared or cross-cutting changes |
| Shared DTO/enum/constant | Build Shared, API, and Web; focused contract tests | Full solution build and test |
| Blazor UI | Build Web; rendered flow and console/network checks | Full solution build when Shared/API changed |
| Auth/authorization/security | Focused negative tests with two users/workspaces | Full test suite and security specialist review |
| EF query/model | API build, focused data/concurrency tests, SQL inspection when material | Migration review and full suite for model changes |
| Migration | Clean API build, inspect Up/Down/designer/snapshot | Disposable DB apply/SQL inspection and full tests by risk |
| Email/storage/notifications/jobs | Provider unit tests plus affected integration tests | Full suite for cross-cutting side effects |
| Performance | Same-scenario baseline and after measurement plus correctness tests | Affected build/test/format gates |
| Container | Local image build and minimal startup/health check when Docker exists | Azure review for deployment claims |
| CI diagnosis | Exact failing CI command reproduced locally when possible | Remote check rerun required before claiming CI success |
| Release | Full required build/test/format, specialist reviews, rollback and monitoring | Exact CI results for release commit |

## Repository commands

```powershell
dotnet restore Planora.slnx
dotnet build Planora.slnx
dotnet test Planora.slnx
dotnet format Planora.slnx --verify-no-changes
git diff --check
git diff
git status --short --branch
```

Use `dotnet test Planora.slnx --filter "FullyQualifiedName~..." --verbosity normal` for focused integration work.

## Completion evidence

- Reproduce the original issue again or exercise the requested behavior directly.
- Report exact commands and outcomes; do not collapse skipped checks into “validated.”
- Inspect authorization, validation, errors, data safety, performance, and provider failure paths proportional to the change.
- Confirm no secrets, generated build output, unrelated edits, or accidental contract changes entered the diff.
