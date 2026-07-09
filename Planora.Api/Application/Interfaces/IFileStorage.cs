namespace Planora.Api.Application.Interfaces;

/// <summary>
/// Abstracts persistence of uploaded files (board cover images today; card
/// attachments later) so the storage backend — local disk in dev, Azure Blob in
/// production — can be swapped without touching controllers. Callers stay
/// responsible for validating content (size, type, magic bytes) before saving.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Persists <paramref name="content"/> under <paramref name="relativeDirectory"/> using a
    /// server-generated file name with the given <paramref name="extension"/> (e.g. ".png").
    /// Returns the public relative URL to the stored file (e.g. "/uploads/boards/{guid}.png").
    /// </summary>
    Task<string> SaveAsync(Stream content, string relativeDirectory, string extension, CancellationToken ct = default);

    /// <summary>
    /// Deletes the file previously returned by <see cref="SaveAsync"/>. No-op when the URL is
    /// null/empty or does not resolve to a file owned by this storage.
    /// </summary>
    Task DeleteAsync(string? relativeUrl, CancellationToken ct = default);

    /// <summary>
    /// Turns a stored file reference (as returned by <see cref="SaveAsync"/>) into a URL a browser
    /// can fetch. Local disk returns it unchanged (served by static files); the Azure Blob backend
    /// signs it into a short-lived read SAS URL because the container is private. Returns the input
    /// unchanged for null/empty values and for legacy/foreign URLs it doesn't own.
    /// </summary>
    string? GetReadUrl(string? storedUrl);
}
