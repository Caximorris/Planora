using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Planora.Shared.DTOs.Auth;
using Planora.Shared.DTOs.Invitation;
using Planora.Shared.DTOs.Workspace;
using Planora.Shared.Enums;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Workspaces;

[Collection("Integration")]
public class WorkspaceLifecycleTests(PlanoraWebAppFactory factory)
{
    private const string Password = "Password1";

    [Fact]
    public async Task Owner_can_transfer_ownership_then_leave_workspace()
    {
        var (owner, ownerAuth) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync("Transfer Workspace");

        var targetEmail = $"new.owner.{Guid.NewGuid():N}@planora.test";
        var (target, targetAuth) = await RegisterAndAuthenticateAsync(targetEmail);
        var invitation = await CreateInvitationAsync(owner, workspace.Id, targetEmail);
        var accept = await target.PostAsync($"/api/invitations/{invitation.Token}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var transfer = await owner.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/transfer-ownership",
            new TransferWorkspaceOwnershipRequest { NewOwnerUserId = targetAuth.UserId });
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);

        var members = await target.GetFromJsonAsync<List<WorkspaceMemberDto>>($"/api/workspaces/{workspace.Id}/members");
        Assert.NotNull(members);
        var memberList = members!;
        Assert.Equal(WorkspaceRole.Owner, memberList.Single(m => m.UserId == targetAuth.UserId).Role);
        Assert.Equal(WorkspaceRole.Admin, memberList.Single(m => m.UserId == ownerAuth.UserId).Role);

        var leave = await owner.PostAsync($"/api/workspaces/{workspace.Id}/leave", null);
        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);

        var oldOwnerRead = await owner.GetAsync($"/api/workspaces/{workspace.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, oldOwnerRead.StatusCode);

        var newOwnerRead = await target.GetAsync($"/api/workspaces/{workspace.Id}");
        Assert.Equal(HttpStatusCode.OK, newOwnerRead.StatusCode);
    }

    [Fact]
    public async Task Non_owner_cannot_transfer_workspace_ownership()
    {
        var (owner, ownerAuth) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync("Blocked Transfer Workspace");

        var memberEmail = $"member.{Guid.NewGuid():N}@planora.test";
        var (member, _) = await RegisterAndAuthenticateAsync(memberEmail);
        var invitation = await CreateInvitationAsync(owner, workspace.Id, memberEmail);
        var accept = await member.PostAsync($"/api/invitations/{invitation.Token}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var transfer = await member.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/transfer-ownership",
            new TransferWorkspaceOwnershipRequest { NewOwnerUserId = ownerAuth.UserId });

        Assert.Equal(HttpStatusCode.Forbidden, transfer.StatusCode);
    }

    [Fact]
    public async Task Sole_owner_cannot_leave_workspace()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync("Sole Owner Workspace");

        var leave = await owner.PostAsync($"/api/workspaces/{workspace.Id}/leave", null);

        Assert.Equal(HttpStatusCode.BadRequest, leave.StatusCode);

        var stillMember = await owner.GetAsync($"/api/workspaces/{workspace.Id}");
        Assert.Equal(HttpStatusCode.OK, stillMember.StatusCode);
    }

    [Fact]
    public async Task Owner_can_revoke_pending_invitation_and_token_cannot_be_accepted()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync("Revoke Invitation Workspace");
        var inviteeEmail = $"revoked.{Guid.NewGuid():N}@planora.test";
        var invitation = await CreateInvitationAsync(owner, workspace.Id, inviteeEmail);

        var revoke = await owner.DeleteAsync($"/api/workspaces/{workspace.Id}/invitations/{invitation.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var lookup = await factory.CreateClient().GetFromJsonAsync<InvitationDto>($"/api/invitations/{invitation.Token}");
        Assert.Equal(InvitationStatus.Revoked, lookup!.Status);

        var (invitee, inviteeAuth) = await RegisterAndAuthenticateAsync(inviteeEmail);
        var accept = await invitee.PostAsync($"/api/invitations/{invitation.Token}/accept", null);
        Assert.Equal(HttpStatusCode.BadRequest, accept.StatusCode);

        var members = await owner.GetFromJsonAsync<List<WorkspaceMemberDto>>($"/api/workspaces/{workspace.Id}/members");
        Assert.DoesNotContain(members!, m => m.UserId == inviteeAuth.UserId);
    }

    [Fact]
    public async Task Member_cannot_revoke_workspace_invitation()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync("Revoke Guard Workspace");

        var memberEmail = $"member.{Guid.NewGuid():N}@planora.test";
        var (member, _) = await RegisterAndAuthenticateAsync(memberEmail);
        var memberInvite = await CreateInvitationAsync(owner, workspace.Id, memberEmail);
        var accept = await member.PostAsync($"/api/invitations/{memberInvite.Token}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var pending = await CreateInvitationAsync(owner, workspace.Id, $"pending.{Guid.NewGuid():N}@planora.test");

        var revoke = await member.DeleteAsync($"/api/workspaces/{workspace.Id}/invitations/{pending.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, revoke.StatusCode);
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> RegisterAndAuthenticateAsync(string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            DisplayName = "Workspace User",
            Email = email,
            Password = Password
        });
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        var authenticated = factory.CreateClient();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (authenticated, auth);
    }

    private static async Task<InvitationDto> CreateInvitationAsync(
        HttpClient owner,
        Guid workspaceId,
        string inviteeEmail,
        WorkspaceRole role = WorkspaceRole.Member)
    {
        var response = await owner.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/invitations",
            new CreateInvitationRequest { InviteeEmail = inviteeEmail, Role = role });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvitationDto>())!;
    }
}
