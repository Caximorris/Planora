using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Planora.Api.Domain.Entities;
using Planora.Shared.DTOs.Auth;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Auth;

/// <summary>
/// Password reset flow (forgot-password + reset-password). Reset tokens are
/// single-use and rotate the SecurityStamp (invalidating existing JWTs); the
/// forgot-password endpoint never reveals whether an account exists.
/// </summary>
[Collection("Integration")]
public class PasswordResetTests(PlanoraWebAppFactory factory)
{
    private const string NewPassword = "NewPassword1";

    private static RegisterRequest NewUser() => AuthTestHelpers.NewUser();

    private async Task<string> RegisterUserAsync(HttpClient client, RegisterRequest user)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", user);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.Token;
    }

    // Generates a real reset token via the running host's UserManager (same data-protection
    // keys as the API), so tests don't depend on parsing it out of the email body.
    private async Task<string> GenerateResetTokenAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync(email);
        return await userManager.GeneratePasswordResetTokenAsync(user!);
    }

    [Fact]
    public async Task Forgot_password_sends_reset_email_for_known_user()
    {
        var client = factory.CreateClient();
        var user = NewUser();
        await RegisterUserAsync(client, user);

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password", new ForgotPasswordRequest { Email = user.Email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var email = factory.EmailSender.Sent.SingleOrDefault(m =>
            m.To == user.Email && m.Subject == "Reset your Planora password");
        Assert.NotNull(email);
        Assert.Contains("/reset-password", email!.Body);
    }

    [Fact]
    public async Task Forgot_password_does_not_reveal_unknown_accounts()
    {
        var client = factory.CreateClient();
        var unknownEmail = $"nobody.{Guid.NewGuid():N}@planora.test";

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password", new ForgotPasswordRequest { Email = unknownEmail });

        // Same 200 as the known-user case, and no email is sent.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(factory.EmailSender.Sent, m => m.To == unknownEmail);
    }

    [Fact]
    public async Task Reset_password_with_valid_token_lets_user_login_with_new_password()
    {
        var client = factory.CreateClient();
        var user = NewUser();
        await RegisterUserAsync(client, user);
        var token = await GenerateResetTokenAsync(user.Email);

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Email = user.Email, Token = token, NewPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = user.Email, Password = NewPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Reset_password_invalidates_existing_access_tokens()
    {
        var client = factory.CreateClient();
        var user = NewUser();
        var oldJwt = await RegisterUserAsync(client, user);

        var token = await GenerateResetTokenAsync(user.Email);
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Email = user.Email, Token = token, NewPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // The pre-reset JWT is rejected because the SecurityStamp rotated on password change.
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldJwt);
        var response = await authed.GetAsync("/api/workspaces");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reset_password_token_is_single_use()
    {
        var client = factory.CreateClient();
        var user = NewUser();
        await RegisterUserAsync(client, user);
        var token = await GenerateResetTokenAsync(user.Email);

        var first = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Email = user.Email, Token = token, NewPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Email = user.Email, Token = token, NewPassword = "AnotherPass2" });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task Reset_password_with_invalid_token_is_rejected()
    {
        var client = factory.CreateClient();
        var user = NewUser();
        await RegisterUserAsync(client, user);

        var response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Email = user.Email, Token = "not-a-valid-token", NewPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_password_clears_an_existing_lockout()
    {
        var client = factory.CreateClient();
        var user = NewUser();
        await RegisterUserAsync(client, user);

        // Trip the lockout (3 failed attempts).
        for (var i = 0; i < 3; i++)
            await client.PostAsJsonAsync(
                "/api/auth/login", new LoginRequest { Email = user.Email, Password = "Wrong9999" });

        var token = await GenerateResetTokenAsync(user.Email);
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Email = user.Email, Token = token, NewPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // Lockout cleared → the new password logs in (200, not 429).
        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = user.Email, Password = NewPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}
