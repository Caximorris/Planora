using System.Net;
using System.Net.Http.Json;
using Planora.Shared.DTOs.Account;
using Planora.Shared.DTOs.Card;
using Planora.Shared.DTOs.Column;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Account;

/// <summary>
/// The data-export endpoint returns the caller's profile plus every workspace they belong to, with
/// full board/column/card content — and must never include a workspace the caller is not a member
/// of. Enforces the plan's isolation acceptance ("export excludes other workspaces' data").
/// </summary>
[Collection("Integration")]
public class AccountExportTests(PlanoraWebAppFactory factory)
{
    [Fact]
    public async Task Export_includes_own_content_and_excludes_other_users_workspaces()
    {
        var (mine, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await mine.CreateWorkspaceAsync("Alpha Workspace");
        var board = await mine.CreateBoardAsync(workspace.Id, "Alpha Board");
        var column = await CreateColumnAsync(mine, board.Id, "Todo");
        var card = await CreateCardAsync(mine, column.Id, "Alpha Card");

        // A second user with their own, unrelated workspace that must never leak into my export.
        var (other, _) = await factory.RegisterAndAuthenticateAsync();
        await other.CreateWorkspaceAsync("Beta Workspace");

        var export = await mine.GetFromJsonAsync<AccountExportDto>("/api/users/export");

        Assert.NotNull(export);
        Assert.False(string.IsNullOrWhiteSpace(export!.User.Email));

        // Every user is seeded with a "Welcome Workspace" on registration, so scope to the one we made.
        var exportedWorkspace = Assert.Single(export.Workspaces, w => w.Name == "Alpha Workspace");
        Assert.True(exportedWorkspace.IsOwner);
        Assert.Equal("Owner", exportedWorkspace.Role);

        var exportedBoard = Assert.Single(exportedWorkspace.Boards);
        Assert.Equal("Alpha Board", exportedBoard.Name);
        var exportedColumn = Assert.Single(exportedBoard.Columns);
        Assert.Equal("Todo", exportedColumn.Title);
        var exportedCard = Assert.Single(exportedColumn.Cards);
        Assert.Equal(card.Id, exportedCard.Id);
        Assert.Equal("Alpha Card", exportedCard.Title);

        Assert.DoesNotContain(export.Workspaces, w => w.Name == "Beta Workspace");
    }

    [Fact]
    public async Task Export_requires_authentication()
    {
        var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync("/api/users/export");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
}
