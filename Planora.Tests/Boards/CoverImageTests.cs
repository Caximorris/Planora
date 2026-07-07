using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Planora.Shared.DTOs.Board;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Boards;

/// <summary>
/// Board cover-image upload after the <see cref="Planora.Api.Application.Interfaces.IFileStorage"/>
/// extraction. Behavior must be unchanged: members can upload a valid image, content is
/// magic-byte validated, and access is workspace-scoped.
/// </summary>
[Collection("Integration")]
public class CoverImageTests(PlanoraWebAppFactory factory)
{
    // A minimal valid 1x1 PNG (correct 8-byte signature + chunks).
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    private static MultipartFormDataContent PngUpload(byte[] bytes, string contentType = "image/png")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", "cover.png");
        return content;
    }

    [Fact]
    public async Task Member_can_upload_a_valid_cover_image()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync();
        var board = await client.CreateBoardAsync(workspace.Id);

        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/cover-image", PngUpload(Convert.FromBase64String(OnePixelPngBase64)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<BoardDto>();
        Assert.NotNull(updated);
        Assert.StartsWith("/uploads/boards/", updated!.CoverImageUrl);
    }

    [Fact]
    public async Task Upload_with_mismatched_content_is_rejected()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync();
        var board = await client.CreateBoardAsync(workspace.Id);

        // Claims image/png but the bytes are not a PNG → magic-byte check fails.
        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/cover-image", PngUpload("this is not a png"u8.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Nonmember_cannot_upload_a_cover_image()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync();
        var board = await owner.CreateBoardAsync(workspace.Id);

        var (outsider, _) = await factory.RegisterAndAuthenticateAsync();
        var response = await outsider.PostAsync(
            $"/api/boards/{board.Id}/cover-image", PngUpload(Convert.FromBase64String(OnePixelPngBase64)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
