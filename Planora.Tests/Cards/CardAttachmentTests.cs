using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.DTOs.Card;
using Planora.Shared.DTOs.Column;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Cards;

[Collection("Integration")]
public class CardAttachmentTests(PlanoraWebAppFactory factory)
{
    private static readonly byte[] MinimalPdf = "%PDF-1.4\n1 0 obj\n<<>>\nendobj\n%%EOF"u8.ToArray();

    [Fact]
    public async Task Member_can_upload_attachment_and_card_detail_includes_it()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Attachment Workspace");
        var board = await client.CreateBoardAsync(workspace.Id, "Attachment Board");
        var column = await CreateColumnAsync(client, board.Id);
        var card = await CreateCardAsync(client, column.Id);

        var response = await client.PostAsync(
            $"/api/cards/{card.Id}/attachments", FileUpload(MinimalPdf, "application/pdf", "spec.pdf"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attachment = (await response.Content.ReadFromJsonAsync<CardAttachmentDto>())!;
        Assert.Equal(card.Id, attachment.CardId);
        Assert.Equal("spec.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.StartsWith("/uploads/cards/", attachment.Url);

        var detail = await client.GetFromJsonAsync<CardDto>($"/api/cards/{card.Id}");
        Assert.Contains(detail!.Attachments, a => a.Id == attachment.Id);
    }

    [Fact]
    public async Task Upload_with_mismatched_content_is_rejected()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Bad Attachment Workspace");
        var board = await client.CreateBoardAsync(workspace.Id, "Bad Attachment Board");
        var column = await CreateColumnAsync(client, board.Id);
        var card = await CreateCardAsync(client, column.Id);

        var response = await client.PostAsync(
            $"/api/cards/{card.Id}/attachments", FileUpload("not a pdf"u8.ToArray(), "application/pdf", "fake.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Nonmember_cannot_upload_or_delete_attachments()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync("Scoped Attachment Workspace");
        var board = await owner.CreateBoardAsync(workspace.Id, "Scoped Attachment Board");
        var column = await CreateColumnAsync(owner, board.Id);
        var card = await CreateCardAsync(owner, column.Id);

        var upload = await owner.PostAsync(
            $"/api/cards/{card.Id}/attachments", FileUpload(MinimalPdf, "application/pdf", "owner.pdf"));
        upload.EnsureSuccessStatusCode();
        var attachment = (await upload.Content.ReadFromJsonAsync<CardAttachmentDto>())!;

        var (outsider, _) = await factory.RegisterAndAuthenticateAsync();
        var outsiderUpload = await outsider.PostAsync(
            $"/api/cards/{card.Id}/attachments", FileUpload(MinimalPdf, "application/pdf", "outsider.pdf"));
        var outsiderDelete = await outsider.DeleteAsync($"/api/cards/{card.Id}/attachments/{attachment.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, outsiderUpload.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, outsiderDelete.StatusCode);
    }

    [Fact]
    public async Task Permanent_card_delete_cascades_attachment_rows()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Cascade Attachment Workspace");
        var board = await client.CreateBoardAsync(workspace.Id, "Cascade Attachment Board");
        var column = await CreateColumnAsync(client, board.Id);
        var card = await CreateCardAsync(client, column.Id);

        var upload = await client.PostAsync(
            $"/api/cards/{card.Id}/attachments", FileUpload(MinimalPdf, "application/pdf", "cascade.pdf"));
        upload.EnsureSuccessStatusCode();
        var attachment = (await upload.Content.ReadFromJsonAsync<CardAttachmentDto>())!;

        await client.DeleteAsync($"/api/cards/{card.Id}");
        var permanent = await client.DeleteAsync($"/api/cards/{card.Id}/permanent");

        Assert.Equal(HttpStatusCode.NoContent, permanent.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.CardAttachments.IgnoreQueryFilters().AnyAsync(a => a.Id == attachment.Id));
    }

    private static MultipartFormDataContent FileUpload(byte[] bytes, string contentType, string fileName)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    private static async Task<ColumnDto> CreateColumnAsync(HttpClient client, Guid boardId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/columns", new CreateColumnRequest { BoardId = boardId, Title = "Todo" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ColumnDto>())!;
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client, Guid columnId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/cards", new CreateCardRequest { ColumnId = columnId, Title = "Attach me" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CardDto>())!;
    }
}
