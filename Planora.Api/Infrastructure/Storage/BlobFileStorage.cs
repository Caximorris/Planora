using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Planora.Api.Application.Interfaces;
using Planora.Api.Application.Options;

namespace Planora.Api.Infrastructure.Storage;

/// <summary>
/// Durable <see cref="IFileStorage"/> backed by Azure Blob Storage, used in production where the
/// container filesystem is ephemeral (a restart/deploy/scale-out on Container Apps would otherwise
/// lose uploaded board covers and card attachments). Selected by <c>Storage:Provider = AzureBlob</c>.
///
/// Blobs are stored as virtual paths (<c>boards/{guid}.png</c>, <c>cards/{guid}.pdf</c>) inside one
/// container and served by anonymous blob read — matching the behavior of the local
/// <c>UseStaticFiles</c> backend, where uploads under <c>wwwroot/uploads</c> are already public by
/// URL. Only the API writes; blobs are never publicly writable. The unguessable GUID name is the
/// same access model the app has always used for uploads.
/// </summary>
public sealed class BlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _container;
    private readonly string _publicBaseUrl;

    public BlobFileStorage(IOptions<StorageOptions> options)
    {
        var blob = options.Value.Blob;
        if (string.IsNullOrWhiteSpace(blob.ConnectionString))
            throw new InvalidOperationException(
                "Storage:Provider is 'AzureBlob' but Storage:Blob:ConnectionString is missing. " +
                "Provide it via environment/secret (see docs/azure-blob-storage.md).");

        _container = new BlobContainerClient(blob.ConnectionString, blob.ContainerName);
        // Create once at startup (singleton). PublicAccessType.Blob = anonymous read of individual
        // blobs (not container listing), so <img src>/download links resolve without SAS — parity
        // with the local static-file backend. Requires the storage account to allow public blob
        // access; if that is disabled, switch to SAS/API-proxied reads (a future enhancement).
        _container.CreateIfNotExists(PublicAccessType.Blob);

        _publicBaseUrl = blob.PublicBaseUrl.TrimEnd('/');
    }

    public async Task<string> SaveAsync(Stream content, string relativeDirectory, string extension, CancellationToken ct = default)
    {
        // Server-generated name (GUID + caller-validated extension); the client name is never used,
        // so there is no traversal risk.
        var blobName = $"{NormalizeDirectory(relativeDirectory)}/{Guid.NewGuid()}{extension}";
        var client = _container.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders { ContentType = ContentTypeForExtension(extension) };
        await client.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, ct);

        return BuildPublicUrl(blobName, client.Uri);
    }

    public async Task DeleteAsync(string? relativeUrl, CancellationToken ct = default)
    {
        // No-op on null/empty, on legacy on-disk URLs ("/uploads/..." — not blobs we own), and on
        // any URL that does not resolve into this container. Mirrors LocalFileStorage's guard.
        if (!TryGetBlobName(relativeUrl, _container.Name, out var blobName)) return;
        await _container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);
    }

    /// <summary>
    /// Builds the blob name prefix from the caller's relative directory. Strips a leading
    /// <c>uploads/</c> segment (a wwwroot-serving convention on the local backend) so blobs live at
    /// <c>boards/…</c> / <c>cards/…</c> inside the container rather than <c>uploads/boards/…</c>,
    /// avoiding a doubled path when the container itself is named "uploads".
    /// </summary>
    public static string NormalizeDirectory(string relativeDirectory)
    {
        var dir = relativeDirectory.Replace('\\', '/').Trim('/');
        if (dir.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            dir = dir["uploads/".Length..];
        return dir.Trim('/');
    }

    /// <summary>Public URL for a stored blob: the configured base + blob name, or the blob URI when no base is set.</summary>
    public static string BuildPublicUrl(string publicBaseUrl, string blobName) =>
        $"{publicBaseUrl.TrimEnd('/')}/{blobName}";

    private string BuildPublicUrl(string blobName, Uri blobUri) =>
        _publicBaseUrl.Length == 0 ? blobUri.ToString() : BuildPublicUrl(_publicBaseUrl, blobName);

    /// <summary>
    /// Extracts the blob name from a stored URL by locating the <c>/{containerName}/</c> segment.
    /// Returns false (so callers no-op) for null/empty, non-absolute legacy disk paths
    /// ("/uploads/boards/x.png"), and URLs that do not contain this container.
    /// </summary>
    public static bool TryGetBlobName(string? url, string containerName, out string blobName)
    {
        blobName = "";
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

        var marker = $"/{containerName}/";
        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        var idx = path.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return false;

        blobName = path[(idx + marker.Length)..].Trim('/');
        return blobName.Length > 0;
    }

    /// <summary>Maps a file extension to the blob <c>Content-Type</c> so images render inline and downloads type correctly.</summary>
    public static string ContentTypeForExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            _ => "application/octet-stream",
        };
}
