namespace Planora.Api.Application.Options;

/// <summary>
/// Binds the <c>Storage</c> configuration section that selects the <see cref="Interfaces.IFileStorage"/>
/// backend. Local disk is the default (dev); production sets <see cref="Provider"/> to
/// <c>AzureBlob</c> and fills <see cref="Blob"/>. The Blob backend is not implemented yet — see
/// <c>docs/azure-blob-storage.md</c> for the remaining steps.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>"Local" (default) or "AzureBlob".</summary>
    public string Provider { get; set; } = "Local";

    public BlobStorageOptions Blob { get; set; } = new();
}

/// <summary>
/// Azure Blob settings, only consumed when <see cref="StorageOptions.Provider"/> is <c>AzureBlob</c>.
/// Real values come from environment/secret (never committed) — the connection string is sensitive.
/// </summary>
public sealed class BlobStorageOptions
{
    /// <summary>Azure Storage connection string (secret; injected via env/Key Vault in prod).</summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>Blob container that holds uploads (e.g. "uploads"). Created if missing at impl time.</summary>
    public string ContainerName { get; set; } = "uploads";

    /// <summary>
    /// Public base URL for stored objects (container or CDN endpoint), used to build the returned
    /// file URL. Existing on-disk URLs ("/uploads/boards/{guid}.png") must keep resolving — the
    /// Blob impl reads both (dual-read) during migration.
    /// </summary>
    public string PublicBaseUrl { get; set; } = "";
}
