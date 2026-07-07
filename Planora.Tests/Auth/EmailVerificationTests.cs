using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Planora.Api.Domain.Entities;
using Planora.Shared.DTOs.Auth;
using Planora.Shared.DTOs.Users;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Auth;

[Collection("Integration")]
public class EmailVerificationTests(PlanoraWebAppFactory factory)
{
    private static RegisterRequest NewUser() => AuthTestHelpers.NewUser();

    private async Task<AuthResponse> RegisterUserAsync(HttpClient client, RegisterRequest user)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", user);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task<string> GenerateConfirmationTokenAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync(email);
        return await userManager.GenerateEmailConfirmationTokenAsync(user!);
    }

    private async Task<bool> IsEmailConfirmedAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync(email);
        return user!.EmailConfirmed;
    }

    [Fact]
    public async Task Register_sends_verification_email_without_blocking_login()
    {
        var client = factory.CreateClient();
        var user = NewUser();

        await RegisterUserAsync(client, user);

        var email = factory.EmailSender.Sent.SingleOrDefault(m =>
            m.To == user.Email && m.Subject == "Verify your Planora email");
        Assert.NotNull(email);
        Assert.Contains("/confirm-email", email!.Body);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = user.Email, Password = user.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_can_resend_verification_email()
    {
        var client = factory.CreateClient();
        var user = NewUser();
        var auth = await RegisterUserAsync(client, user);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var response = await client.PostAsync("/api/auth/send-email-confirmation", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(factory.EmailSender.Sent.Count(m =>
            m.To == user.Email && m.Subject == "Verify your Planora email") >= 2);
    }

    [Fact]
    public async Task Confirm_email_with_valid_token_marks_profile_verified()
    {
        var client = factory.CreateClient();
        var user = NewUser();
        var auth = await RegisterUserAsync(client, user);
        var token = await GenerateConfirmationTokenAsync(user.Email);

        var confirm = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest { UserId = auth.UserId, Token = token });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.True(await IsEmailConfirmedAsync(user.Email));

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var profile = await authed.GetFromJsonAsync<UserProfileDto>("/api/users/profile");
        Assert.NotNull(profile);
        Assert.True(profile!.EmailConfirmed);
    }

    [Fact]
    public async Task Confirm_email_with_invalid_token_is_rejected()
    {
        var client = factory.CreateClient();
        var user = NewUser();
        var auth = await RegisterUserAsync(client, user);

        var confirm = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest { UserId = auth.UserId, Token = "bad-token" });

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        Assert.False(await IsEmailConfirmedAsync(user.Email));
    }
}
