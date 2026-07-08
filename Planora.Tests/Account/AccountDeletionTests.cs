using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.DTOs.Account;
using Planora.Shared.DTOs.Auth;
using Planora.Shared.DTOs.Invitation;
using Planora.Shared.DTOs.Workspace;
using Planora.Shared.Enums;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Account;

/// <summary>
/// Permanent account deletion: re-authenticates with the password, removes solo-owned workspaces
/// with the account, and blocks (409) when the user still owns a workspace shared with other
/// members. Deleting the account also strips its memberships from other people's workspaces without
/// touching those workspaces.
/// </summary>
[Collection("Integration")]
public class AccountDeletionTests(PlanoraWebAppFactory factory)
{
    private const string Password = "Password1";

    [Fact]
    public async Task Delete_with_wrong_password_is_rejected()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/users/delete-account",
            new DeleteAccountRequest { Password = "WrongPassword1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Account is untouched — the token still works.
        var profile = await client.GetAsync("/api/users/profile");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task Delete_is_blocked_when_owning_a_workspace_with_other_members()
    {
        var (owner, ownerAuth) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync("Shared Workspace");

        var memberEmail = $"member.{Guid.NewGuid():N}@planora.test";
        var (member, _) = await RegisterAndAuthenticateAsync(memberEmail);
        var invitation = await CreateInvitationAsync(owner, workspace.Id, memberEmail);
        var accept = await member.PostAsync($"/api/invitations/{invitation.Token}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var response = await owner.PostAsJsonAsync("/api/users/delete-account",
            new DeleteAccountRequest { Password = Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var blocked = await response.Content.ReadFromJsonAsync<AccountDeletionBlockedResponse>();
        Assert.NotNull(blocked);
        var blockedWorkspace = Assert.Single(blocked!.BlockedWorkspaces);
        Assert.Equal(workspace.Id, blockedWorkspace.Id);
        Assert.Equal(2, blockedWorkspace.MemberCount);

        // Nothing was deleted: the owner's account and the workspace both survive.
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/users/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/workspaces/{workspace.Id}")).StatusCode);
        Assert.True(await UserExistsAsync(ownerAuth.UserId));
    }

    [Fact]
    public async Task Delete_removes_the_user_and_their_solo_owned_workspace()
    {
        var (client, auth) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync("Solo Workspace");
        await client.CreateBoardAsync(workspace.Id, "Solo Board");

        var response = await client.PostAsJsonAsync("/api/users/delete-account",
            new DeleteAccountRequest { Password = Password });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // The user and their solo workspace are gone from the database...
        Assert.False(await UserExistsAsync(auth.UserId));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Workspaces.AnyAsync(w => w.Id == workspace.Id));
        }

        // ...and the now-orphaned token is rejected (SecurityStamp re-check finds no user).
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/profile")).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_member_removes_their_membership_but_keeps_the_owners_workspace()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync("Owner Workspace");

        var memberEmail = $"member.{Guid.NewGuid():N}@planora.test";
        var (member, memberAuth) = await RegisterAndAuthenticateAsync(memberEmail);
        var invitation = await CreateInvitationAsync(owner, workspace.Id, memberEmail);
        var accept = await member.PostAsync($"/api/invitations/{invitation.Token}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        // The member owns nothing shared, so deletion succeeds.
        var response = await member.PostAsJsonAsync("/api/users/delete-account",
            new DeleteAccountRequest { Password = Password });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // The workspace still exists and no longer lists the deleted member.
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/workspaces/{workspace.Id}")).StatusCode);
        var members = await owner.GetFromJsonAsync<List<WorkspaceMemberDto>>($"/api/workspaces/{workspace.Id}/members");
        Assert.DoesNotContain(members!, m => m.UserId == memberAuth.UserId);
        Assert.False(await UserExistsAsync(memberAuth.UserId));
    }

    private async Task<bool> UserExistsAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.AnyAsync(u => u.Id == userId);
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> RegisterAndAuthenticateAsync(string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            DisplayName = "Member User",
            Email = email,
            Password = Password
        });
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        var authenticated = factory.CreateClient();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (authenticated, auth);
    }

    private static async Task<InvitationDto> CreateInvitationAsync(HttpClient owner, Guid workspaceId, string inviteeEmail)
    {
        var response = await owner.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/invitations",
            new CreateInvitationRequest { InviteeEmail = inviteeEmail, Role = WorkspaceRole.Member });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvitationDto>())!;
    }
}
