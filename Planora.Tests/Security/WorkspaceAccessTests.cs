using System.Net;
using System.Net.Http.Json;
using Planora.Shared.DTOs.Board;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Security;

/// <summary>
/// IDOR / workspace-membership authorization. A user who is not a member of a
/// workspace must never be able to read or mutate its boards — the check lives at
/// the service/data boundary in every controller. Non-members get 403 (Forbid);
/// unauthenticated callers get 401.
/// </summary>
[Collection("Integration")]
public class WorkspaceAccessTests(PlanoraWebAppFactory factory)
{
    [Fact]
    public async Task Owner_can_read_own_workspace()
    {
        var (client, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await client.CreateWorkspaceAsync();

        var response = await client.GetAsync($"/api/workspaces/{workspace.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Nonmember_cannot_read_another_users_workspace()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync();

        var (outsider, _) = await factory.RegisterAndAuthenticateAsync();
        var response = await outsider.GetAsync($"/api/workspaces/{workspace.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Nonmember_cannot_list_another_workspaces_boards()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync();

        var (outsider, _) = await factory.RegisterAndAuthenticateAsync();
        var response = await outsider.GetAsync($"/api/workspaces/{workspace.Id}/boards");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Nonmember_cannot_read_another_users_board()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync();
        var board = await owner.CreateBoardAsync(workspace.Id);

        var (outsider, _) = await factory.RegisterAndAuthenticateAsync();
        var response = await outsider.GetAsync($"/api/boards/{board.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Nonmember_cannot_create_board_in_foreign_workspace()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync();

        var (outsider, _) = await factory.RegisterAndAuthenticateAsync();
        var response = await outsider.PostAsJsonAsync(
            "/api/boards", new CreateBoardRequest { Name = "Intruder", WorkspaceId = workspace.Id });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_is_unauthorized()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync();
        var workspace = await owner.CreateWorkspaceAsync();

        var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/workspaces/{workspace.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
