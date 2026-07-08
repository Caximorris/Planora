using System.Net;
using System.Net.Http.Json;
using Planora.Shared.DTOs.Board;
using Planora.Shared.DTOs.Card;
using Planora.Shared.DTOs.Column;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Concurrency;

[Collection("Integration")]
public class OptimisticConcurrencyTests(PlanoraWebAppFactory factory)
{
    [Fact]
    public async Task Stale_board_update_returns_conflict_and_preserves_current_value()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Board Concurrency WS");
        var board = await client.CreateBoardAsync(workspace.Id, "Original board");

        var freshResponse = await client.PutAsJsonAsync($"/api/boards/{board.Id}", new UpdateBoardRequest
        {
            RowVersion = board.RowVersion,
            Name = "Fresh board"
        });
        freshResponse.EnsureSuccessStatusCode();
        var fresh = (await freshResponse.Content.ReadFromJsonAsync<BoardDto>())!;
        Assert.NotEqual(board.RowVersion, fresh.RowVersion);

        var staleResponse = await client.PutAsJsonAsync($"/api/boards/{board.Id}", new UpdateBoardRequest
        {
            RowVersion = board.RowVersion,
            Name = "Stale board"
        });

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        var reloaded = await client.GetFromJsonAsync<BoardDetailDto>($"/api/boards/{board.Id}");
        Assert.Equal("Fresh board", reloaded!.Name);
    }

    [Fact]
    public async Task Stale_column_update_returns_conflict_and_preserves_current_value()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Column Concurrency WS");
        var board = await client.CreateBoardAsync(workspace.Id, "Column Concurrency Board");
        var column = await CreateColumnAsync(client, board.Id, "Original column");

        var freshResponse = await client.PutAsJsonAsync($"/api/columns/{column.Id}", new UpdateColumnRequest
        {
            RowVersion = column.RowVersion,
            Title = "Fresh column"
        });
        freshResponse.EnsureSuccessStatusCode();
        var fresh = (await freshResponse.Content.ReadFromJsonAsync<ColumnDto>())!;
        Assert.NotEqual(column.RowVersion, fresh.RowVersion);

        var staleResponse = await client.PutAsJsonAsync($"/api/columns/{column.Id}", new UpdateColumnRequest
        {
            RowVersion = column.RowVersion,
            Title = "Stale column"
        });

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        var reloaded = await client.GetFromJsonAsync<BoardDetailDto>($"/api/boards/{board.Id}");
        Assert.Equal("Fresh column", reloaded!.Columns.Single(c => c.Id == column.Id).Title);
    }

    [Fact]
    public async Task Stale_card_update_returns_conflict_and_preserves_current_value()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Card Concurrency WS");
        var board = await client.CreateBoardAsync(workspace.Id, "Card Concurrency Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Original card");

        var freshResponse = await client.PutAsJsonAsync($"/api/cards/{card.Id}", new UpdateCardRequest
        {
            RowVersion = card.RowVersion,
            Title = "Fresh card"
        });
        freshResponse.EnsureSuccessStatusCode();
        var fresh = (await freshResponse.Content.ReadFromJsonAsync<CardDto>())!;
        Assert.NotEqual(card.RowVersion, fresh.RowVersion);

        var staleResponse = await client.PutAsJsonAsync($"/api/cards/{card.Id}", new UpdateCardRequest
        {
            RowVersion = card.RowVersion,
            Title = "Stale card"
        });

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        var reloaded = await client.GetFromJsonAsync<CardDto>($"/api/cards/{card.Id}");
        Assert.Equal("Fresh card", reloaded!.Title);
    }

    private static async Task<ColumnDto> CreateColumnAsync(HttpClient client, Guid boardId, string title)
    {
        var response = await client.PostAsJsonAsync(
            "/api/columns", new CreateColumnRequest { BoardId = boardId, Title = title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ColumnDto>())!;
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client, Guid columnId, string title)
    {
        var response = await client.PostAsJsonAsync(
            "/api/cards", new CreateCardRequest { ColumnId = columnId, Title = title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CardDto>())!;
    }
}
