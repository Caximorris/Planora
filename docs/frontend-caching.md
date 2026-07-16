# Frontend deployment caching

## Root cause

Planora had revalidation rules for several files, but its deployment did not create a clean, explicit publish artifact or identify the build a browser had loaded. More importantly, a navigation-fallback route such as `/workspaces` was not covered by an explicit cache rule, and existing users could still be controlled by a historical offline service worker. That allowed an old application shell to request a mixture of old and newly deployed resources, which can leave the browser on an unstyled startup/authentication screen.

## Policy

`staticwebapp.config.json` evaluates route rules in order. Its global header establishes
revalidation as the default, including SPA navigation-fallback responses; specific asset routes
override that default. The policy is therefore:

- `index.html`, all SPA fallback paths, `version.json`, `appsettings.json`, service-worker scripts, and stable Blazor boot/runtime files use `Cache-Control: no-cache, must-revalidate`.
- Fingerprinted framework, stylesheet, and vendored-library assets use `Cache-Control: public, max-age=31536000, immutable`.
- Directly loaded JavaScript uses fingerprinted immutable URLs. `cover-cropper.js` is dynamically imported with a stable URL, so its exact route uses revalidation before the immutable `/js/*` rule. Never change that route to immutable unless the import is converted to a content-addressed URL.

The .NET static-asset fingerprint pattern and `#[.{fingerprint}]` references in `index.html` give directly loaded JavaScript a content-addressed filename. Stylesheets intentionally retain their publish paths because Blazor does not rewrite their `href` values; the workflow appends the current commit SHA as a build-generated query parameter instead. Do not add a stable local asset to an immutable route unless its URL changes whenever its content changes.

## PWA migration

Planora does not provide an offline product mode, and it must not cache authenticated API responses, tokens, or private workspace data. New registrations are not created. The two retained `service-worker*.js` files are one-time retirement workers for historical deployments: they activate, delete only known Planora/Blazor PWA cache prefixes, and unregister. They intentionally do not reload open tabs, because that could discard active edits. The next navigation or the update prompt loads the normal network-backed application.

## Version detection

The workflow writes the commit SHA into `appsettings.json` before publish and writes `version.json` into the clean publish output. The client compares its loaded build version with an unauthenticated `fetch` equivalent that uses `Cache-Control: no-cache, no-store` every 15 minutes and when a tab becomes visible. On a mismatch it presents **Update now**; it never reloads automatically.

## Deployment and verification

The GitHub workflow restores and publishes `Planora.Web` into `artifacts/planora-web`, creates `version.json`, validates the artifact with `scripts/Test-FrontendCaching.ps1`, then deploys that exact directory with `skip_app_build: true`. This prevents stale source or output from another build being merged into the deployment.

After a deployment, verify the real headers without relying on browser DevTools cache state:

```powershell
scripts/Test-FrontendCacheHeaders.ps1 -SiteUrl https://planora.website
```

For a local publish artifact, run:

```powershell
dotnet publish Planora.Web/Planora.Web.csproj -c Release -o artifacts/planora-web
scripts/Test-FrontendCaching.ps1 -PublishDirectory artifacts/planora-web/wwwroot
```
