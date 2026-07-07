using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Planora.Shared.DTOs.Auth;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Auth;

/// <summary>
/// Refresh-token rotation and reuse detection. Each refresh rotates the token: the
/// presented token is consumed and a new one issued, so replaying the old token fails.
/// A revoked token that is presented again (e.g. after logout) is rejected and triggers
/// RevokeAllAsync for that user in RefreshTokenService — the theft-containment path.
/// </summary>
[Collection("Integration")]
public class RefreshTokenTests(PlanoraWebAppFactory factory)
{
    private static async Task<AuthResponse> RefreshAsync(HttpClient client, string refreshToken)
    {
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(refreshToken));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    [Fact]
    public async Task Refresh_rotates_the_token()
    {
        var client = factory.CreateClient();
        var auth = await factory.RegisterAsync();

        var rotated = await RefreshAsync(client, auth.RefreshToken);

        Assert.False(string.IsNullOrWhiteSpace(rotated.RefreshToken));
        Assert.NotEqual(auth.RefreshToken, rotated.RefreshToken);
    }

    [Fact]
    public async Task Old_token_is_rejected_after_rotation()
    {
        var client = factory.CreateClient();
        var auth = await factory.RegisterAsync();

        var rotated = await RefreshAsync(client, auth.RefreshToken);

        // Replaying the consumed original token fails...
        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // ...while the freshly rotated token still works.
        var second = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(rotated.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_garbage_token_is_unauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest("not-a-real-refresh-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refreshing_a_revoked_token_after_logout_is_rejected()
    {
        var client = factory.CreateClient();
        var auth = await factory.RegisterAsync();

        // Logout revokes the refresh token (without deleting it), so presenting it again
        // hits the reuse-detection branch in RefreshTokenService.
        var logout = await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sessions_list_marks_the_current_refresh_token()
    {
        var client = factory.CreateClient();
        var auth = await factory.RegisterAsync();
        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = auth.Email, Password = "Password1" });
        login.EnsureSuccessStatusCode();
        var secondSession = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondSession.Token);
        var response = await client.PostAsJsonAsync("/api/auth/sessions", new SessionListRequest
        {
            CurrentRefreshToken = secondSession.RefreshToken
        });

        response.EnsureSuccessStatusCode();
        var sessions = await response.Content.ReadFromJsonAsync<List<RefreshSessionDto>>();
        Assert.NotNull(sessions);
        Assert.True(sessions!.Count >= 2);
        Assert.Single(sessions, s => s.IsCurrent);
    }

    [Fact]
    public async Task Revoking_one_session_invalidates_that_refresh_token_only()
    {
        var client = factory.CreateClient();
        var auth = await factory.RegisterAsync();
        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = auth.Email, Password = "Password1" });
        login.EnsureSuccessStatusCode();
        var current = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", current.Token);
        var sessionsResponse = await client.PostAsJsonAsync("/api/auth/sessions", new SessionListRequest
        {
            CurrentRefreshToken = current.RefreshToken
        });
        sessionsResponse.EnsureSuccessStatusCode();
        var sessions = (await sessionsResponse.Content.ReadFromJsonAsync<List<RefreshSessionDto>>())!;
        var oldSession = sessions.Single(s => !s.IsCurrent);

        var revoke = await client.PostAsJsonAsync(
            "/api/auth/sessions/revoke", new RevokeSessionRequest { SessionId = oldSession.Id });
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var currentRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(current.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, currentRefresh.StatusCode);

        var oldRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
    }

    [Fact]
    public async Task Revoke_others_keeps_current_session_active()
    {
        var client = factory.CreateClient();
        var auth = await factory.RegisterAsync();
        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = auth.Email, Password = "Password1" });
        login.EnsureSuccessStatusCode();
        var current = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", current.Token);
        var revoke = await client.PostAsJsonAsync("/api/auth/sessions/revoke-others", new RevokeOtherSessionsRequest
        {
            CurrentRefreshToken = current.RefreshToken
        });
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var currentRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(current.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, currentRefresh.StatusCode);

        var oldRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
    }

    [Fact]
    public async Task Cannot_revoke_another_users_session()
    {
        var userA = await factory.RegisterAsync();
        var userB = await factory.RegisterAsync();
        var clientA = factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userA.Token);

        var clientB = factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userB.Token);
        var sessionsResponse = await clientB.PostAsJsonAsync("/api/auth/sessions", new SessionListRequest
        {
            CurrentRefreshToken = userB.RefreshToken
        });
        sessionsResponse.EnsureSuccessStatusCode();
        var bSession = (await sessionsResponse.Content.ReadFromJsonAsync<List<RefreshSessionDto>>())!.Single(s => s.IsCurrent);

        var revoke = await clientA.PostAsJsonAsync(
            "/api/auth/sessions/revoke", new RevokeSessionRequest { SessionId = bSession.Id });

        Assert.Equal(HttpStatusCode.NotFound, revoke.StatusCode);
    }
}
