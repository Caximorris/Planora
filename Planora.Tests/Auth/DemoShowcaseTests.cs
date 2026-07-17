using System.Net.Http.Headers;
using System.Net.Http.Json;
using Planora.Shared.DTOs.Auth;
using Planora.Shared.DTOs.Board;
using Planora.Shared.DTOs.Card;
using Planora.Shared.DTOs.Label;
using Planora.Shared.DTOs.Workspace;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Auth;

[Collection("Integration")]
public sealed class DemoShowcaseTests
{
    private readonly PlanoraWebAppFactory _factory;

    public DemoShowcaseTests(PlanoraWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Demo_account_receives_a_complete_multi_workspace_showcase()
    {
        using var anonymous = _factory.CreateClient();
        var authResponse = await anonymous.PostAsync("/api/auth/demo", content: null);
        authResponse.EnsureSuccessStatusCode();
        var auth = (await authResponse.Content.ReadFromJsonAsync<AuthResponse>())!;

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var workspaces = (await client.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces"))!;
        Assert.Equal(3, workspaces.Count);
        Assert.Equal(3, workspaces.Select(w => w.Name).Distinct().Count());

        foreach (var workspace in workspaces)
        {
            var boards = (await client.GetFromJsonAsync<List<BoardDto>>($"/api/workspaces/{workspace.Id}/boards?includeArchived=true"))!;
            Assert.Equal(3, boards.Count);

            var members = (await client.GetFromJsonAsync<List<WorkspaceMemberDto>>($"/api/workspaces/{workspace.Id}/members"))!;
            Assert.Equal(4, members.Count);
            Assert.Contains(members, member => member.Role == Planora.Shared.Enums.WorkspaceRole.Owner);
            Assert.Equal(4, members.Select(member => member.UserId).Distinct().Count());

            var labels = (await client.GetFromJsonAsync<List<LabelDto>>($"/api/labels/workspace/{workspace.Id}"))!;
            Assert.Equal(5, labels.Count);

            var invitation = await client.GetFromJsonAsync<List<object>>($"/api/workspaces/{workspace.Id}/invitations");
            Assert.NotNull(invitation);
            Assert.NotEmpty(invitation!);
        }

        var firstWorkspace = workspaces.Single(workspace => workspace.Name == "Northstar Product Lab");
        var activeBoard = (await client.GetFromJsonAsync<List<BoardDto>>($"/api/workspaces/{firstWorkspace.Id}/boards"))!
            .First();
        var detail = (await client.GetFromJsonAsync<BoardDetailDto>($"/api/boards/{activeBoard.Id}"))!;
        var cards = detail.Columns.SelectMany(column => column.Cards).ToList();
        Assert.Equal(15, cards.Count);
        Assert.Contains(cards, card => card.Priority == Planora.Shared.Enums.CardPriority.Critical);
        Assert.All(cards, card => Assert.NotEmpty(card.Labels));

        var cardWithDetails = cards.Single(card => card.Title.Contains("align on scope", StringComparison.Ordinal)
            && card.Title.EndsWith("(3)", StringComparison.Ordinal));
        var card = (await client.GetFromJsonAsync<CardDto>($"/api/cards/{cardWithDetails.Id}"))!;
        Assert.NotEmpty(card.Checklists);
        Assert.NotEmpty(card.Checklists[0].Items);

        var comments = (await client.GetFromJsonAsync<List<CardCommentDto>>($"/api/cards/{card.Id}/comments"))!;
        Assert.Equal(2, comments.Count);

        var archivedBoard = (await client.GetFromJsonAsync<List<BoardDto>>($"/api/workspaces/{firstWorkspace.Id}/boards?includeArchived=true"))!
            .Single(board => board.IsArchived);
        var archivedDetail = await client.GetFromJsonAsync<BoardDetailDto>($"/api/boards/{archivedBoard.Id}?includeArchived=true");
        Assert.NotNull(archivedDetail);
        Assert.Contains(archivedDetail!.Columns.SelectMany(column => column.Cards), card => card.IsArchived);

        var notifications = await client.GetFromJsonAsync<List<object>>("/api/notifications");
        Assert.NotNull(notifications);
        Assert.NotEmpty(notifications!);
    }
}
