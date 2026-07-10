# Azure Blob storage — implementation handoff (Tasks 8–9)

> **Status (2026-07-09): implemented, private + SAS.** `BlobFileStorage`
> (`Planora.Api/Infrastructure/Storage/BlobFileStorage.cs`) is the durable backend, selected by
> `Storage:Provider = AzureBlob` in `Program.cs`. The container is **private** (no anonymous access);
> reads go out as short-lived signed SAS URLs minted by `GetReadUrl` and applied to every response by
> `MediaUrlResolutionFilter` — a browser only ever gets a working URL through an API response it was
> already authorized to receive, and that URL expires (`Storage:Blob:SasMinutes`, default 60).
> Dual-read is automatic (see Task 9 below). Verified by `dotnet build Planora.slnx` (clean) and
> `Planora.Tests/Storage/` (139 tests green). **Production is configured (2026-07-10):**
> `deploy-api.yml` sets `Storage__Provider=AzureBlob`,
> `Storage__Blob__ConnectionString=secretref:storage-connection-string`, and
> `Storage__Blob__PublicBaseUrl=https://planorabs.blob.core.windows.net/uploads`. The sections
> below are retained as the design record.

## Why this exists

Uploaded files (board cover images and card attachments) are written to the container's
local disk under `wwwroot/uploads/boards` and `wwwroot/uploads/cards`. On Azure Container Apps that filesystem is **ephemeral** —
every restart, deploy, or scale-out **loses the files**. `IFileStorage`
(`Planora.Api/Application/Interfaces/IFileStorage.cs`) already abstracts save/delete so the backend
can be swapped without touching the board/card upload controllers. Only the durable backend is missing.

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
   (unchanged — validation stays in the controller), and delete-on-board/card permanent-delete. Blob round-trips
   need either Azurite (local emulator) in CI or a thin fake `IFileStorage`; do not hit real Azure
   from tests.

### Task 9 — dual-read / migration ✅

**Chosen: dual-read, and it needs no extra code.** Both frontend resolvers
(`BoardService.ResolveImageUrl`, `CardService.ResolveAttachmentUrl`) build the final URL with
`new Uri(_http.BaseAddress!, storedUrl)`, whose behavior is: a **relative** legacy path
(`/uploads/boards/{guid}.png`) resolves against the API origin and is served by `UseStaticFiles`;
an **absolute** Blob URL is returned unchanged. So legacy disk covers and new Blob covers both render
with zero frontend changes. On the backend, `BlobFileStorage.DeleteAsync` no-ops for legacy
`/uploads/...` values (they aren't blobs it owns — verified by `TryGetBlobName` tests), so deleting a
board that predates the cutover never misfires against the container.

**Acceptance:** a cover uploaded in prod survives a container restart (durable blob); existing boards
still show their covers (dual-read above); dev still works on disk (`Provider: "Local"`, the default).

## Azure resources to provision (outside the repo)

Do this once; no code changes needed. Portal steps (or the `az` equivalents):

1. **Create a Storage Account** — same resource group/region as the Container App; Standard/LRS is fine.
2. **Create a private container** named `uploads` (access level **Private** — the default; the app
   keeps it private and signs SAS URLs for reads, so anonymous access must stay off).
3. **Copy the connection string** — Security + networking → Access keys → key1 connection string.
4. **Set three Container Apps settings** (store the connection string as a *secret*):
   - `Storage__Provider = AzureBlob`
   - `Storage__Blob__ConnectionString = <secret>`  (double-underscore = nested config key)
   - `Storage__Blob__PublicBaseUrl = https://<acct>.blob.core.windows.net/uploads`

The connection string carries the account key, which `BlobFileStorage` needs to **mint read SAS
URLs** (`client.GenerateSasUri`). A key-less Managed Identity would require user-delegation SAS
instead — a later enhancement; `GetReadUrl` degrades to the stored (unfetchable-from-a-private-
container) URL if it ever runs without a shared-key credential, so switching auth models means
revisiting SAS generation.

## Config reference

```jsonc
"Storage": {
  "Provider": "AzureBlob",              // "Local" (default) | "AzureBlob"
  "Blob": {
    "ConnectionString": "<secret>",     // env/Key Vault only — never commit; account key signs SAS
    "ContainerName": "uploads",
    "PublicBaseUrl": "https://<acct>.blob.core.windows.net/uploads",
    "SasMinutes": 60                    // read-SAS lifetime; long enough for an open board
  }
}
```
