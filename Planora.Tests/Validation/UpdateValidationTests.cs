using System.Net;
using System.Net.Http.Json;
using Planora.Shared.DTOs.Board;
using Planora.Shared.DTOs.Card;
using Planora.Shared.DTOs.Column;
using Planora.Shared.DTOs.Label;
using Planora.Shared.DTOs.Workspace;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Validation;

/// <summary>
/// Update endpoints now run FluentValidation (parity with the create paths): names/titles have
/// length and non-empty limits even on partial updates. These pin that invalid shapes are rejected
/// with 400 and that valid updates still succeed.
/// </summary>
[Collection("Integration")]
public class UpdateValidationTests(PlanoraWebAppFactory factory)
{
    [Fact]
    public async Task Board_update_with_over_long_name_is_rejected()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Validation WS");
        var board = await client.CreateBoardAsync(workspace.Id, "Original");

        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}", new UpdateBoardRequest
        {
            RowVersion = board.RowVersion,
            Name = new string('x', 101)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Board_update_with_valid_name_still_succeeds()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Validation WS");
        var board = await client.CreateBoardAsync(workspace.Id, "Original");

        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}", new UpdateBoardRequest
        {
            RowVersion = board.RowVersion,
            Name = "Renamed Board"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<BoardDto>())!;
        Assert.Equal("Renamed Board", updated.Name);
    }

    [Fact]
    public async Task Card_update_with_empty_title_is_rejected()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Validation WS");
        var board = await client.CreateBoardAsync(workspace.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Original card");

        var response = await client.PutAsJsonAsync($"/api/cards/{card.Id}", new UpdateCardRequest
        {
            RowVersion = card.RowVersion,
            Title = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Workspace_update_with_over_long_name_is_rejected()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Validation WS");

        var response = await client.PutAsJsonAsync($"/api/workspaces/{workspace.Id}", new UpdateWorkspaceRequest
        {
            Name = new string('x', 101)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Label_update_with_over_long_name_is_rejected()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Validation WS");
        var label = await CreateLabelAsync(client, workspace.Id, "Bug");

        var response = await client.PutAsJsonAsync($"/api/labels/{label.Id}", new UpdateLabelRequest
        {
            Name = new string('x', 51)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<ColumnDto> CreateColumnAsync(HttpClient client, Guid boardId, string title)
    {
        var res = await client.PostAsJsonAsync("/api/columns", new CreateColumnRequest { BoardId = boardId, Title = title });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ColumnDto>())!;
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client, Guid columnId, string title)
    {
        var res = await client.PostAsJsonAsync("/api/cards", new CreateCardRequest { ColumnId = columnId, Title = title });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CardDto>())!;
    }

    private static async Task<LabelDto> CreateLabelAsync(HttpClient client, Guid workspaceId, string name)
    {
        var res = await client.PostAsJsonAsync($"/api/labels/workspace/{workspaceId}", new CreateLabelRequest { Name = name });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<LabelDto>())!;
    }
}
