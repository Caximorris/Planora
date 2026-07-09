using Planora.Api.Infrastructure.Storage;

namespace Planora.Tests.Storage;

/// <summary>
/// Unit coverage for the pure URL/blob-name logic of <see cref="BlobFileStorage"/> — the parts that
/// are easy to get wrong (path shape, dual-read delete guard, content types) and don't need a live
/// Azure account or emulator. Blob round-trips against real storage are out of scope here
/// (see docs/azure-blob-storage.md).
/// </summary>
public class BlobFileStorageTests
{
    [Theory]
    [InlineData("uploads/boards", "boards")]
    [InlineData("uploads/cards", "cards")]
    [InlineData("/uploads/boards/", "boards")]
    [InlineData("UPLOADS/boards", "boards")]       // case-insensitive strip
    [InlineData("boards", "boards")]                // no uploads prefix → unchanged
    [InlineData("uploads\\boards", "boards")]      // backslashes normalized
    public void NormalizeDirectory_strips_uploads_prefix_and_slashes(string input, string expected)
    {
        Assert.Equal(expected, BlobFileStorage.NormalizeDirectory(input));
    }

    [Fact]
    public void BuildPublicUrl_joins_base_and_blob_name()
    {
        var url = BlobFileStorage.BuildPublicUrl("https://acct.blob.core.windows.net/uploads/", "boards/abc.png");
        Assert.Equal("https://acct.blob.core.windows.net/uploads/boards/abc.png", url);
    }

    [Fact]
    public void TryGetBlobName_extracts_name_from_a_stored_blob_url()
    {
        var ok = BlobFileStorage.TryGetBlobName(
            "https://acct.blob.core.windows.net/uploads/boards/abc.png", "uploads", out var name);

        Assert.True(ok);
        Assert.Equal("boards/abc.png", name);
    }

    [Fact]
    public void TryGetBlobName_ignores_legacy_on_disk_urls()
    {
        // Dual-read: old boards still hold relative "/uploads/..." paths. Deleting one must no-op,
        // not misfire against the blob container.
        var ok = BlobFileStorage.TryGetBlobName("/uploads/boards/abc.png", "uploads", out var name);

        Assert.False(ok);
        Assert.Equal("", name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://other.example.com/photos/abc.png")]  // absolute but not our container
    [InlineData("file:///uploads/boards/abc.png")]            // non-http scheme (Linux file:// parse of a legacy path)
    public void TryGetBlobName_returns_false_for_empty_or_foreign_urls(string? url)
    {
        Assert.False(BlobFileStorage.TryGetBlobName(url, "uploads", out _));
    }

    [Theory]
    [InlineData(".png", "image/png")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".WEBP", "image/webp")]   // case-insensitive
    [InlineData(".gif", "image/gif")]
    [InlineData(".pdf", "application/pdf")]
    [InlineData(".txt", "text/plain")]
    [InlineData(".bin", "application/octet-stream")]
    public void ContentTypeForExtension_maps_uploads_to_correct_type(string extension, string expected)
    {
        Assert.Equal(expected, BlobFileStorage.ContentTypeForExtension(extension));
    }
}
