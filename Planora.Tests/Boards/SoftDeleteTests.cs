using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.DTOs.Board;
using Planora.Shared.DTOs.Calendar;
using Planora.Shared.DTOs.Card;
using Planora.Shared.DTOs.Column;
using Planora.Shared.DTOs.Search;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Boards;

/// <summary>
/// Soft-delete (trash) for boards and cards: DELETE moves a row to trash (recoverable), the
/// global query filter hides it everywhere, restore brings it back, and permanent-delete removes
/// it for good. These tests pin the filter-consistency guarantee — a trashed row must never leak
/// through GetById, search, or the calendar — and the cross-workspace access guards.
/// </summary>
[Collection("Integration")]
public class SoftDeleteTests(PlanoraWebAppFactory factory)
{
    private static async Task<ColumnDto> CreateColumnAsync(HttpClient client, Guid boardId, string title = "Todo")
    {
        var response = await client.PostAsJsonAsync(
            "/api/columns", new CreateColumnRequest { BoardId = boardId, Title = title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ColumnDto>())!;
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client, Guid columnId, string title = "Card", DateTime? due = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/cards", new CreateCardRequest { ColumnId = columnId, Title = title, DueDate = due });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CardDto>())!;
    }

    // ---- Boards -------------------------------------------------------------------------------

    [Fact]
    public async Task Deleting_board_moves_it_to_trash_and_hides_it_everywhere()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Trash WS");
        var board = await client.CreateBoardAsync(workspace.Id, "Doomed Board");

        var delete = await client.DeleteAsync($"/api/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // Gone from the normal board list and GetById...
        var list = await client.GetFromJsonAsync<List<BoardDto>>($"/api/workspaces/{workspace.Id}/boards");
        Assert.DoesNotContain(list!, b => b.Id == board.Id);

        var get = await client.GetAsync($"/api/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        // ...and from the archived view (includeArchived must not surface trash)...
        var archivedView = await client.GetFromJsonAsync<List<BoardDto>>(
            $"/api/workspaces/{workspace.Id}/boards?includeArchived=true");
        Assert.DoesNotContain(archivedView!, b => b.Id == board.Id);

        // ...but present in the workspace trash, with DeletedAt set.
        var trash = await client.GetFromJsonAsync<List<BoardDto>>($"/api/boards/trash?workspaceId={workspace.Id}");
        var trashed = Assert.Single(trash!, b => b.Id == board.Id);
        Assert.NotNull(trashed.DeletedAt);
    }

    [Fact]
    public async Task Restoring_board_brings_it_back_and_clears_trash()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Restore WS");
        var board = await client.CreateBoardAsync(workspace.Id, "Comeback Board");
        await client.DeleteAsync($"/api/boards/{board.Id}");

        var restore = await client.PatchAsync($"/api/boards/{board.Id}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        var list = await client.GetFromJsonAsync<List<BoardDto>>($"/api/workspaces/{workspace.Id}/boards");
        Assert.Contains(list!, b => b.Id == board.Id);

        var trash = await client.GetFromJsonAsync<List<BoardDto>>($"/api/boards/trash?workspaceId={workspace.Id}");
        Assert.DoesNotContain(trash!, b => b.Id == board.Id);
    }

    [Fact]
    public async Task Permanently_deleting_trashed_board_removes_the_row()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Purge WS");
        var board = await client.CreateBoardAsync(workspace.Id, "Purge Board");
        await client.DeleteAsync($"/api/boards/{board.Id}");

        var permanent = await client.DeleteAsync($"/api/boards/{board.Id}/permanent");
        Assert.Equal(HttpStatusCode.NoContent, permanent.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exists = await db.Boards.IgnoreQueryFilters().AnyAsync(b => b.Id == board.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task Permanent_delete_rejects_a_live_board()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Live WS");
        var board = await client.CreateBoardAsync(workspace.Id, "Live Board");

        // Only trashed boards can be permanently deleted.
        var permanent = await client.DeleteAsync($"/api/boards/{board.Id}/permanent");
        Assert.Equal(HttpStatusCode.NotFound, permanent.StatusCode);
    }

    [Fact]
    public async Task Trashed_board_cards_disappear_from_search_and_calendar()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Consistency WS");
        var board = await client.CreateBoardAsync(workspace.Id, "Consistency Board");
        var column = await CreateColumnAsync(client, board.Id);
        var card = await CreateCardAsync(client, column.Id, "ZZUniqueCardXYZ", DateTime.UtcNow);

        await client.DeleteAsync($"/api/boards/{board.Id}");

        var results = await client.GetFromJsonAsync<List<SearchResultDto>>("/api/search?q=ZZUniqueCardXYZ");
        Assert.DoesNotContain(results!, r => r.Id == card.Id);

        var month = DateTime.UtcNow.ToString("yyyy-MM");
        var calendar = await client.GetFromJsonAsync<List<CalendarCardDto>>(
            $"/api/workspaces/{workspace.Id}/calendar?month={month}");
        Assert.DoesNotContain(calendar!, c => c.Id == card.Id);
    }

    // ---- Cards --------------------------------------------------------------------------------

    [Fact]
    public async Task Deleting_card_trashes_it_and_hides_it_even_with_includeArchived()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Card Trash WS");
        var board = await client.CreateBoardAsync(workspace.Id, "Card Trash Board");
        var column = await CreateColumnAsync(client, board.Id);
        var card = await CreateCardAsync(client, column.Id, "Trash me", DateTime.UtcNow);

        var delete = await client.DeleteAsync($"/api/cards/{card.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // A trashed card must never appear on the board, even when archived cards are requested.
        var detail = await client.GetFromJsonAsync<BoardDetailDto>($"/api/boards/{board.Id}?includeArchived=true");
        Assert.DoesNotContain(detail!.Columns.SelectMany(c => c.Cards), c => c.Id == card.Id);

        // Not in search or calendar either.
        var month = DateTime.UtcNow.ToString("yyyy-MM");
        var calendar = await client.GetFromJsonAsync<List<CalendarCardDto>>(
            $"/api/workspaces/{workspace.Id}/calendar?month={month}");
        Assert.DoesNotContain(calendar!, c => c.Id == card.Id);

        // But present in the board's card trash.
        var trash = await client.GetFromJsonAsync<List<CardDto>>($"/api/cards/trash?boardId={board.Id}");
        Assert.Contains(trash!, c => c.Id == card.Id);
    }

    [Fact]
    public async Task Restoring_card_returns_it_to_its_column()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Card Restore WS");
        var board = await client.CreateBoardAsync(workspace.Id, "Card Restore Board");
        var column = await CreateColumnAsync(client, board.Id);
        var card = await CreateCardAsync(client, column.Id, "Bring me back");
        await client.DeleteAsync($"/api/cards/{card.Id}");

        var restore = await client.PatchAsync($"/api/cards/{card.Id}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        var detail = await client.GetFromJsonAsync<BoardDetailDto>($"/api/boards/{board.Id}");
        Assert.Contains(detail!.Columns.SelectMany(c => c.Cards), c => c.Id == card.Id);
    }

    [Fact]
    public async Task Permanently_deleting_trashed_card_removes_the_row()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Card Purge WS");
        var board = await client.CreateBoardAsync(workspace.Id, "Card Purge Board");
        var column = await CreateColumnAsync(client, board.Id);
        var card = await CreateCardAsync(client, column.Id);
        await client.DeleteAsync($"/api/cards/{card.Id}");

        var permanent = await client.DeleteAsync($"/api/cards/{card.Id}/permanent");
        Assert.Equal(HttpStatusCode.NoContent, permanent.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exists = await db.Cards.IgnoreQueryFilters().AnyAsync(c => c.Id == card.Id);
        Assert.False(exists);
    }

    // ---- Access control -----------------------------------------------------------------------

    [Fact]
    public async Task Non_member_cannot_list_trash_restore_or_permanently_delete()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync("Guarded WS");
        var board = await owner.CreateBoardAsync(workspace.Id, "Guarded Board");
        await owner.DeleteAsync($"/api/boards/{board.Id}");

        var (outsider, _) = await factory.RegisterAndAuthenticateAsync();

        var trash = await outsider.GetAsync($"/api/boards/trash?workspaceId={workspace.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, trash.StatusCode);

        var restore = await outsider.PatchAsync($"/api/boards/{board.Id}/restore", null);
        Assert.Equal(HttpStatusCode.Forbidden, restore.StatusCode);

        var permanent = await outsider.DeleteAsync($"/api/boards/{board.Id}/permanent");
        Assert.Equal(HttpStatusCode.Forbidden, permanent.StatusCode);
    }
}
