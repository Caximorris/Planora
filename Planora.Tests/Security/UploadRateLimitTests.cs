using System.Net;
using System.Net.Http.Headers;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Security;

/// <summary>
/// Upload endpoints carry the per-user "uploads" rate-limit policy. The limit is set to 10/min
/// for tests via RateLimiting__UploadPermitLimit in <see cref="PlanoraWebAppFactory"/>. The
/// partition key is the authenticated user id, so one user exhausting their window must not
/// affect anyone else.
/// </summary>
[Collection("Integration")]
public class UploadRateLimitTests(PlanoraWebAppFactory factory)
{
    private const int TestPermitLimit = 10; // keep in sync with PlanoraWebAppFactory

    // A minimal valid 1x1 PNG (correct 8-byte signature + chunks).
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    private static MultipartFormDataContent PngUpload()
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Convert.FromBase64String(OnePixelPngBase64));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "cover.png");
        return content;
    }

    [Fact]
    public async Task Upload_over_the_per_user_limit_returns_429_without_affecting_other_users()
    {
        var (limited, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await limited.CreateWorkspaceAsync();
        var board = await limited.CreateBoardAsync(workspace.Id);

        for (var i = 0; i < TestPermitLimit; i++)
        {
            var ok = await limited.PostAsync($"/api/boards/{board.Id}/cover-image", PngUpload());
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var rejected = await limited.PostAsync($"/api/boards/{board.Id}/cover-image", PngUpload());
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);

        // A different user is on their own partition and uploads fine right away.
        var (other, _) = await factory.RegisterAndAuthenticateAsync();
        var otherWorkspace = await other.CreateWorkspaceAsync();
        var otherBoard = await other.CreateBoardAsync(otherWorkspace.Id);
        var allowed = await other.PostAsync($"/api/boards/{otherBoard.Id}/cover-image", PngUpload());
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }
}
