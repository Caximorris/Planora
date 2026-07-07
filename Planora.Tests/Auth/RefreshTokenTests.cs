using System.Net;
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
}
