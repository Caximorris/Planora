using Planora.Api.Application.Interfaces;

namespace Planora.Api.Infrastructure.Storage;

/// <summary>
/// Stores uploaded files under the web root (wwwroot) so they are served by
/// <c>UseStaticFiles</c>. Used in development; production swaps in a durable
/// backend (Azure Blob) behind <see cref="IFileStorage"/> — the container
/// filesystem is ephemeral and would lose files on restart/scale-out.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly IWebHostEnvironment _env;

    public LocalFileStorage(IWebHostEnvironment env) => _env = env;

    public async Task<string> SaveAsync(Stream content, string relativeDirectory, string extension, CancellationToken ct = default)
    {
        var directory = Path.Combine(_env.WebRootPath, relativeDirectory);
        Directory.CreateDirectory(directory);

        // File name is always server-generated (GUID + caller-validated extension); the
        // client-supplied name is never used for the path, so there is no traversal risk.
        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(directory, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
            await content.CopyToAsync(stream, ct);

        // Public URL is relative to wwwroot; normalize separators for the web.
        return $"/{relativeDirectory.Replace('\\', '/').Trim('/')}/{fileName}";
    }

    public Task DeleteAsync(string? relativeUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl)) return Task.CompletedTask;

        var root = Path.GetFullPath(_env.WebRootPath);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        var candidate = Path.GetFullPath(Path.Combine(root, relativeUrl.TrimStart('/', '\\')));

        // Guard against path traversal — only ever delete inside the web root.
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            return Task.CompletedTask;

        if (File.Exists(candidate)) File.Delete(candidate);
        return Task.CompletedTask;
    }

    // Files under wwwroot are served directly by UseStaticFiles; the stored relative URL is already
    // the browser-fetchable URL, so no transformation is needed.
    public string? GetReadUrl(string? storedUrl) => storedUrl;
}
