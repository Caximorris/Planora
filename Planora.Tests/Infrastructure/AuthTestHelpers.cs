using System.Net.Http.Headers;
using System.Net.Http.Json;
using Planora.Shared.DTOs.Auth;
using Planora.Shared.DTOs.Board;
using Planora.Shared.DTOs.Workspace;

namespace Planora.Tests.Infrastructure;

/// <summary>
/// Shared setup helpers for integration tests. Every user gets a unique GUID email
/// so tests stay independent on the shared (never-reset) test database.
/// </summary>
internal static class AuthTestHelpers
{
    public static RegisterRequest NewUser() => new()
    {
        DisplayName = "Test User",
        Email = $"user.{Guid.NewGuid():N}@planora.test",
        Password = "Password1"
    };

    /// <summary>Registers a brand-new user and returns the raw auth payload (unauthenticated client).</summary>
    public static async Task<AuthResponse> RegisterAsync(this PlanoraWebAppFactory factory)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", NewUser());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    /// <summary>Registers a brand-new user and returns a client with its bearer token attached.</summary>
    public static async Task<(HttpClient Client, AuthResponse Auth)> RegisterAndAuthenticateAsync(
        this PlanoraWebAppFactory factory)
    {
        var auth = await factory.RegisterAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth);
    }

    /// <summary>Creates a workspace owned by the authenticated client and returns it.</summary>
    public static async Task<WorkspaceDto> CreateWorkspaceAsync(this HttpClient client, string name = "Test Workspace")
    {
        var response = await client.PostAsJsonAsync("/api/workspaces", new CreateWorkspaceRequest { Name = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkspaceDto>())!;
    }

    /// <summary>Creates a board in the given workspace and returns it.</summary>
    public static async Task<BoardDto> CreateBoardAsync(this HttpClient client, Guid workspaceId, string name = "Test Board")
    {
        var response = await client.PostAsJsonAsync(
            "/api/boards", new CreateBoardRequest { Name = name, WorkspaceId = workspaceId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BoardDto>())!;
    }
}
