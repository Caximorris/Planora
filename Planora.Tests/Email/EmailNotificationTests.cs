using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.DTOs.Auth;
using Planora.Shared.DTOs.Card;
using Planora.Shared.DTOs.Column;
using Planora.Shared.DTOs.Invitation;
using Planora.Shared.DTOs.Users;
using Planora.Shared.Enums;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Email;

/// <summary>
/// Activity email notifications + per-user preferences. Asserts against the in-memory
/// <see cref="CapturingEmailSender"/> (registered by the test factory) — no real Resend calls.
/// </summary>
[Collection("Integration")]
public class EmailNotificationTests(PlanoraWebAppFactory factory)
{
    private const string AssignedSubject = "You were assigned to a card";
    private const string CommentSubject = "New comment on your card";

    private static async Task<ColumnDto> CreateColumnAsync(HttpClient client, Guid boardId)
    {
        var res = await client.PostAsJsonAsync("/api/columns", new CreateColumnRequest { BoardId = boardId, Title = "Todo" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ColumnDto>())!;
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client, Guid columnId)
    {
        var res = await client.PostAsJsonAsync("/api/cards", new CreateCardRequest { ColumnId = columnId, Title = "Task" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CardDto>())!;
    }

    // Registers a second user and adds them directly as a member of the workspace.
    private async Task<AuthResponse> SeedMemberAsync(Guid workspaceId)
    {
        var member = await factory.RegisterAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = member.UserId,
            Role = WorkspaceRole.Member
        });
        await db.SaveChangesAsync();
        return member;
    }

    private HttpClient AuthedClient(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Assigning_a_card_emails_the_assignee()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync();
        var board = await owner.CreateBoardAsync(workspace.Id);
        var column = await CreateColumnAsync(owner, board.Id);
        var card = await CreateCardAsync(owner, column.Id);
        var member = await SeedMemberAsync(workspace.Id);

        var res = await owner.PutAsJsonAsync($"/api/cards/{card.Id}",
            new UpdateCardRequest { RowVersion = card.RowVersion, AssigneeId = member.UserId });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        Assert.Contains(factory.EmailSender.Sent, m => m.To == member.Email && m.Subject == AssignedSubject);
    }

    [Fact]
    public async Task Assigning_does_not_email_an_assignee_who_opted_out()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync();
        var board = await owner.CreateBoardAsync(workspace.Id);
        var column = await CreateColumnAsync(owner, board.Id);
        var card = await CreateCardAsync(owner, column.Id);
        var member = await SeedMemberAsync(workspace.Id);

        var opt = await AuthedClient(member.Token).PutAsJsonAsync("/api/users/notification-preferences",
            new NotificationPreferencesDto { EmailOnAssigned = false, EmailOnComment = true, EmailOnWorkspaceInvite = true });
        opt.EnsureSuccessStatusCode();

        var res = await owner.PutAsJsonAsync($"/api/cards/{card.Id}",
            new UpdateCardRequest { RowVersion = card.RowVersion, AssigneeId = member.UserId });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        Assert.DoesNotContain(factory.EmailSender.Sent, m => m.To == member.Email && m.Subject == AssignedSubject);
    }

    [Fact]
    public async Task Commenting_emails_the_card_assignee()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync();
        var board = await owner.CreateBoardAsync(workspace.Id);
        var column = await CreateColumnAsync(owner, board.Id);
        var card = await CreateCardAsync(owner, column.Id);
        var member = await SeedMemberAsync(workspace.Id);

        var assign = await owner.PutAsJsonAsync($"/api/cards/{card.Id}",
            new UpdateCardRequest { RowVersion = card.RowVersion, AssigneeId = member.UserId });
        assign.EnsureSuccessStatusCode();

        var comment = await owner.PostAsJsonAsync($"/api/cards/{card.Id}/comments",
            new CreateCommentRequest { Text = "Nice work" });
        comment.EnsureSuccessStatusCode();

        Assert.Contains(factory.EmailSender.Sent, m => m.To == member.Email && m.Subject == CommentSubject);
    }

    [Fact]
    public async Task Creating_an_invitation_emails_the_invitee_with_a_link()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync("Invite Workspace");
        var inviteeEmail = $"invitee.{Guid.NewGuid():N}@planora.test";

        var res = await owner.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/invitations",
            new CreateInvitationRequest { InviteeEmail = inviteeEmail, Role = WorkspaceRole.Member });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var email = factory.EmailSender.Sent.SingleOrDefault(m => m.To == inviteeEmail);
        Assert.NotNull(email);
        Assert.Contains("/invite/", email!.Body);
    }

    [Fact]
    public async Task Notification_preferences_default_on_and_round_trip()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();

        var initial = await client.GetFromJsonAsync<NotificationPreferencesDto>("/api/users/notification-preferences");
        Assert.True(initial!.EmailOnAssigned);
        Assert.True(initial.EmailOnComment);
        Assert.True(initial.EmailOnWorkspaceInvite);

        var update = await client.PutAsJsonAsync("/api/users/notification-preferences",
            new NotificationPreferencesDto { EmailOnAssigned = false, EmailOnComment = false, EmailOnWorkspaceInvite = true });
        update.EnsureSuccessStatusCode();

        var after = await client.GetFromJsonAsync<NotificationPreferencesDto>("/api/users/notification-preferences");
        Assert.False(after!.EmailOnAssigned);
        Assert.False(after.EmailOnComment);
        Assert.True(after.EmailOnWorkspaceInvite);
    }

    [Fact]
    public async Task Notification_preferences_require_authentication()
    {
        var res = await factory.CreateClient().GetAsync("/api/users/notification-preferences");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
