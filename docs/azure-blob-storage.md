# Azure Blob storage — implementation handoff (Tasks 8–9)

> **Status:** groundwork prepared, backend **not implemented**. The deploy-target decision is
> settled: **Azure Blob** (matches the Azure Container Apps hosting). This doc is the exact
> remaining work so implementing it is a single new class plus config — no re-architecture.

## Why this exists

Uploaded files (board cover images today; card attachments next) are written to the container's
local disk under `wwwroot/uploads/boards`. On Azure Container Apps that filesystem is **ephemeral** —
every restart, deploy, or scale-out **loses the files**. `IFileStorage`
(`Planora.Api/Application/Interfaces/IFileStorage.cs`) already abstracts save/delete so the backend
can be swapped without touching `BoardsController`. Only the durable backend is missing.

## What is already wired (prepared, do not redo)

- **`IFileStorage`** — the abstraction; `LocalFileStorage` is the dev impl.
- **`StorageOptions`** (`Planora.Api/Application/Options/StorageOptions.cs`) — binds the `Storage`
  config section (`Provider` + `Blob:{ConnectionString,ContainerName,PublicBaseUrl}`).
- **Config contract** — `appsettings.json` carries an empty `Storage` block (`Provider: "Local"`);
  real Blob values come from env/secret, never committed.
- **Provider seam** in `Program.cs` — a `switch` on `Storage:Provider`. `"Local"` registers
  `LocalFileStorage`; `"AzureBlob"` currently **throws** `NotSupportedException` pointing here.
  Implementing Task 8 = replacing that throw with the `BlobFileStorage` registration.

## Remaining steps

### Task 8 — Blob-backed `IFileStorage`

1. **Add the SDK package** to `Planora.Api.csproj`:
   `<PackageReference Include="Azure.Storage.Blobs" Version="<latest 12.x>" />`
   (verify the current version; nothing else has pulled it in).
2. **Create `Planora.Api/Infrastructure/Storage/BlobFileStorage.cs`** implementing `IFileStorage`:
   - Ctor takes `BlobStorageOptions` (via `IOptions<StorageOptions>`); build a
     `BlobContainerClient` from `ConnectionString` + `ContainerName`.
   - `SaveAsync`: server-generated `{Guid}{extension}` blob name under `relativeDirectory` as a
     virtual path (e.g. `boards/{guid}.png`); `UploadAsync` the stream; set the correct
     `Content-Type` from the extension. Return the public URL built from `PublicBaseUrl`
     (or the blob URI) — **keep the same shape callers store today** so the mapper/DB need no change.
   - `DeleteAsync`: parse the blob name back out of the stored URL and `DeleteIfExistsAsync`.
     No-op on null/empty or a URL not owned by this container (mirror `LocalFileStorage`'s guard).
   - Ensure the container exists once at startup, not per request (create-if-not-exists in ctor
     or a hosted init), and **do not** make blobs publicly writable — only the API writes.
3. **Register it** — replace the `"azureblob"` throw in `Program.cs` with
   `builder.Services.AddSingleton<IFileStorage, BlobFileStorage>();` and bind
   `builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));`
4. **Tests** — mirror `Planora.Tests/Boards/CoverImageTests.cs`: bad magic bytes / oversize rejected
   (unchanged — validation stays in the controller), and delete-on-board-delete. Blob round-trips
   need either Azurite (local emulator) in CI or a thin fake `IFileStorage`; do not hit real Azure
   from tests.

### Task 9 — dual-read / migration

Existing rows hold on-disk URLs like `/uploads/boards/{guid}.png`. After the cutover those files no
longer exist in the container. Choose one, and cover it with a test:

- **Dual-read (recommended):** when resolving a cover URL, if it is a legacy `/uploads/...` path
  serve it via the existing static-files path; new uploads return Blob URLs. Both must render.
- **One-time copy:** a small migration/console step that uploads existing disk files to Blob and
  rewrites `Board.CoverImageUrl`. Simpler long-term, but only viable if the current disk still holds
  the originals at migration time (on ephemeral storage it may not).

**Acceptance:** a cover uploaded in prod survives a container restart; existing boards still show
their covers; dev still works on disk (`Provider: "Local"`).

## Azure resources to provision (outside the repo)

- A Storage Account + a private blob container (name = `Blob:ContainerName`, default `uploads`).
- Container Apps secret carrying `Storage__Blob__ConnectionString` (double-underscore = nested key),
  plus `Storage__Provider=AzureBlob` and `Storage__Blob__PublicBaseUrl`.
- Prefer a Managed Identity + `DefaultAzureCredential` over a connection string if you want to avoid
  a long-lived secret (then `BlobFileStorage` takes the account URL + credential instead).

## Config reference

```jsonc
"Storage": {
  "Provider": "AzureBlob",              // "Local" (default) | "AzureBlob"
  "Blob": {
    "ConnectionString": "<secret>",     // env/Key Vault only — never commit
    "ContainerName": "uploads",
    "PublicBaseUrl": "https://<acct>.blob.core.windows.net/uploads"
  }
}
```
