using System.Net;
using System.Net.Http.Json;
using Planora.Shared.DTOs.Auth;
using Planora.Tests.Infrastructure;

namespace Planora.Tests;

[Collection("Integration")]
public class AuthFlowTests(PlanoraWebAppFactory factory)
{
    private static RegisterRequest NewUser() => new()
    {
        DisplayName = "Test User",
        Email = $"user.{Guid.NewGuid():N}@planora.test",
        Password = "Password1"
    };

    [Fact]
    public async Task Register_then_login_returns_tokens()
    {
        var client = factory.CreateClient();
        var user = NewUser();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", user);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registered);
        Assert.False(string.IsNullOrWhiteSpace(registered!.Token));
        Assert.False(string.IsNullOrWhiteSpace(registered.RefreshToken));

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = user.Email, Password = user.Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loggedIn = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loggedIn);
        Assert.False(string.IsNullOrWhiteSpace(loggedIn!.Token));
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        var client = factory.CreateClient();
        var user = NewUser();
        await client.PostAsJsonAsync("/api/auth/register", user);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = user.Email, Password = "Wrong9999" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_probe_reports_healthy()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
