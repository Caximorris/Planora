using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.DTOs.Activity;
using Planora.Shared.DTOs.Card;
using Planora.Shared.DTOs.Column;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Cards;

[Collection("Integration")]
public class ActivityEventTests(PlanoraWebAppFactory factory)
{
    private static async Task<ColumnDto> CreateColumnAsync(HttpClient client, Guid boardId, string title)
    {
        var response = await client.PostAsJsonAsync(
            "/api/columns", new CreateColumnRequest { BoardId = boardId, Title = title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ColumnDto>())!;
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client, Guid columnId, string title = "Audit card")
    {
        var response = await client.PostAsJsonAsync(
            "/api/cards", new CreateCardRequest { ColumnId = columnId, Title = title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CardDto>())!;
    }

    [Fact]
    public async Task Creating_card_emits_scoped_activity_event()
    {
        var (client, auth) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Activity Workspace");
        var board = await client.CreateBoardAsync(workspace.Id, "Activity Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");

        var card = await CreateCardAsync(client, column.Id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var activity = await db.ActivityEvents.SingleAsync(e =>
            e.TargetId == card.Id && e.Verb == "card.created");

        Assert.Equal(auth.UserId, activity.ActorUserId);
        Assert.Equal("card", activity.TargetType);
        Assert.Equal(workspace.Id, activity.WorkspaceId);
        Assert.Equal(board.Id, activity.BoardId);

        using var payload = JsonDocument.Parse(activity.PayloadJson);
        Assert.Equal(card.Title, payload.RootElement.GetProperty("title").GetString());
        Assert.Equal(column.Id, payload.RootElement.GetProperty("columnId").GetGuid());
    }

    [Fact]
    public async Task Moving_card_emits_activity_event_with_from_and_to_columns()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Move Activity Workspace");
        var board = await client.CreateBoardAsync(workspace.Id, "Move Activity Board");
        var todo = await CreateColumnAsync(client, board.Id, "Todo");
        var done = await CreateColumnAsync(client, board.Id, "Done");
        var card = await CreateCardAsync(client, todo.Id, "Move me");

        var move = await client.PutAsJsonAsync($"/api/cards/{card.Id}", new UpdateCardRequest
        {
            RowVersion = card.RowVersion,
            ColumnId = done.Id,
            Position = 0
        });

        Assert.Equal(HttpStatusCode.OK, move.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var activity = await db.ActivityEvents.SingleAsync(e =>
            e.TargetId == card.Id && e.Verb == "card.moved");

        Assert.Equal(workspace.Id, activity.WorkspaceId);
        Assert.Equal(board.Id, activity.BoardId);

        using var payload = JsonDocument.Parse(activity.PayloadJson);
        Assert.Equal(todo.Id, payload.RootElement.GetProperty("fromColumnId").GetGuid());
        Assert.Equal(done.Id, payload.RootElement.GetProperty("toColumnId").GetGuid());
        Assert.Equal("Todo", payload.RootElement.GetProperty("fromColumnTitle").GetString());
        Assert.Equal("Done", payload.RootElement.GetProperty("toColumnTitle").GetString());
    }

    [Fact]
    public async Task Board_activity_endpoint_returns_newest_first_and_stays_workspace_scoped()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Feed Workspace");
        var board = await client.CreateBoardAsync(workspace.Id, "Feed Board");
        var todo = await CreateColumnAsync(client, board.Id, "Todo");
        var done = await CreateColumnAsync(client, board.Id, "Done");
        var card = await CreateCardAsync(client, todo.Id, "Feed card");

        var move = await client.PutAsJsonAsync($"/api/cards/{card.Id}", new UpdateCardRequest
        {
            RowVersion = card.RowVersion,
            ColumnId = done.Id,
            Position = 0
        });
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);

        var feed = await client.GetFromJsonAsync<List<ActivityEventDto>>($"/api/boards/{board.Id}/activity");
        Assert.NotNull(feed);
        Assert.True(feed!.Count >= 2);
        Assert.Equal("card.moved", feed[0].Verb);
        Assert.Equal("card.created", feed[1].Verb);
        Assert.Contains("Moved", feed[0].Summary);
        Assert.Contains("Created", feed[1].Summary);

        var (otherClient, _) = await factory.RegisterAndAuthenticateAsync();
        var forbidden = await otherClient.GetAsync($"/api/boards/{board.Id}/activity");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
