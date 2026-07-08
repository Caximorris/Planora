using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Planora.Api.Domain.Entities;
using Planora.Shared.DTOs.Auth;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Auth;

/// <summary>
/// Two-factor authentication (TOTP + recovery codes). Enabling 2FA gates login behind a second
/// factor via POST /api/auth/login/2fa, which re-verifies the password and applies the same
/// progressive lockout as the password step. Setup/enable/disable rotate the SecurityStamp, so the
/// helpers refresh the access token between authorized calls (the browser does this automatically
/// via AuthHeaderHandler).
/// </summary>
[Collection("Integration")]
public class TwoFactorTests(PlanoraWebAppFactory factory)
{
    // AuthTestHelpers.NewUser() always uses this password.
    private const string Password = "Password1";

    private HttpClient AuthedClient(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // Exchanges a refresh token for a fresh access token (reflecting the current SecurityStamp) and
    // returns an authed client plus the rotated refresh token for the next hop.
    private async Task<(HttpClient Client, string RefreshToken)> RefreshAsync(string refreshToken)
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(refreshToken));
        res.EnsureSuccessStatusCode();
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
        return (AuthedClient(auth.Token), auth.RefreshToken);
    }

    // Computes the current authenticator TOTP from the user's stored base32 key. Identity's
    // authenticator provider is Google-Authenticator-compatible (30s step, HMACSHA1, 6 digits, no
    // modifier), and its GenerateTwoFactorTokenAsync returns "" for this provider — so we derive the
    // code with the standard RFC 6238 algorithm to match VerifyTwoFactorTokenAsync.
    private async Task<string> CurrentCodeAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync(email);
        var key = await users.GetAuthenticatorKeyAsync(user!);
        return Totp.Generate(key!);
    }

    // Registers a user and fully enables 2FA over HTTP; returns the email, the latest refresh token
    // (for further authed calls), and the recovery codes.
    private async Task<(string Email, string RefreshToken, List<string> RecoveryCodes)> EnableTwoFactorAsync()
    {
        var auth = await factory.RegisterAsync();

        // setup persists the authenticator key (and rotates the stamp).
        var setup = await AuthedClient(auth.Token).PostAsync("/api/auth/2fa/setup", null);
        setup.EnsureSuccessStatusCode();

        // Refresh (stamp rotated), then confirm with a real code to enable.
        var (client, refresh) = await RefreshAsync(auth.RefreshToken);
        var code = await CurrentCodeAsync(auth.Email);
        var enable = await client.PostAsJsonAsync("/api/auth/2fa/enable", new EnableTwoFactorRequest { Code = code });
        enable.EnsureSuccessStatusCode();
        var codes = (await enable.Content.ReadFromJsonAsync<TwoFactorRecoveryCodesResponse>())!.RecoveryCodes;

        return (auth.Email, refresh, codes);
    }

    [Fact]
    public async Task Setup_then_enable_turns_on_2fa_and_returns_ten_recovery_codes()
    {
        var (_, refresh, codes) = await EnableTwoFactorAsync();
        Assert.Equal(10, codes.Count);

        // enable rotated the stamp again → refresh to read status.
        var (client, _) = await RefreshAsync(refresh);
        var status = await client.GetFromJsonAsync<TwoFactorStatusResponse>("/api/auth/2fa/status");
        Assert.True(status!.Enabled);
        Assert.Equal(10, status.RecoveryCodesRemaining);
    }

    [Fact]
    public async Task Login_with_2fa_enabled_requires_second_factor_and_issues_no_token()
    {
        var (email, _, _) = await EnableTwoFactorAsync();

        var res = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = email, Password = Password });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.True(auth.RequiresTwoFactor);
        Assert.True(string.IsNullOrEmpty(auth.Token));
    }

    [Fact]
    public async Task Login_2fa_with_valid_totp_issues_a_token()
    {
        var (email, _, _) = await EnableTwoFactorAsync();
        var code = await CurrentCodeAsync(email);

        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest { Email = email, Password = Password, Code = code });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.False(auth.RequiresTwoFactor);
        Assert.False(string.IsNullOrEmpty(auth.Token));
    }

    [Fact]
    public async Task Login_2fa_with_wrong_code_is_rejected()
    {
        var (email, _, _) = await EnableTwoFactorAsync();

        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest { Email = email, Password = Password, Code = "000000" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_2fa_rejects_a_wrong_password_even_with_a_valid_code()
    {
        var (email, _, _) = await EnableTwoFactorAsync();
        var code = await CurrentCodeAsync(email);

        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest { Email = email, Password = "WrongPass9", Code = code });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Recovery_code_logs_in_once_then_cannot_be_reused()
    {
        var (email, _, codes) = await EnableTwoFactorAsync();
        var recovery = codes[0];

        var first = await factory.CreateClient().PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest { Email = email, Password = Password, Code = recovery, IsRecoveryCode = true });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await factory.CreateClient().PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest { Email = email, Password = Password, Code = recovery, IsRecoveryCode = true });
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task Repeated_wrong_2fa_codes_trigger_progressive_lockout()
    {
        var (email, _, _) = await EnableTwoFactorAsync();
        var client = factory.CreateClient();

        // Lockout begins at the 3rd failed attempt (same thresholds as the password step).
        for (var i = 0; i < 3; i++)
            await client.PostAsJsonAsync("/api/auth/login/2fa",
                new TwoFactorLoginRequest { Email = email, Password = Password, Code = "000000" });

        // Even a correct code is now rejected with 429 while the lockout window is open.
        var code = await CurrentCodeAsync(email);
        var res = await client.PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest { Email = email, Password = Password, Code = code });
        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
    }

    [Fact]
    public async Task Disable_requires_a_valid_code_then_login_skips_2fa()
    {
        var (email, refresh, _) = await EnableTwoFactorAsync();
        var (client, _) = await RefreshAsync(refresh);

        var wrong = await client.PostAsJsonAsync("/api/auth/2fa/disable",
            new DisableTwoFactorRequest { Code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var code = await CurrentCodeAsync(email);
        var ok = await client.PostAsJsonAsync("/api/auth/2fa/disable",
            new DisableTwoFactorRequest { Code = code });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        // Login now issues a token directly — no second step.
        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = email, Password = Password });
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.False(auth.RequiresTwoFactor);
        Assert.False(string.IsNullOrEmpty(auth.Token));
    }

    [Fact]
    public async Task Login_without_2fa_enabled_is_unchanged()
    {
        var auth = await factory.RegisterAsync();

        var res = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = auth.Email, Password = Password });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.False(body.RequiresTwoFactor);
        Assert.False(string.IsNullOrEmpty(body.Token));
    }

    [Fact]
    public async Task Two_factor_endpoints_require_authentication()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/auth/2fa/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/api/auth/2fa/setup", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PostAsJsonAsync("/api/auth/2fa/enable", new EnableTwoFactorRequest { Code = "123456" })).StatusCode);
    }
}

/// <summary>Minimal RFC 6238 TOTP generator for tests (30s step, HMACSHA1, 6 digits, no modifier).</summary>
internal static class Totp
{
    public static string Generate(string base32Key)
    {
        var key = FromBase32(base32Key);
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
                   | ((hash[offset + 1] & 0xff) << 16)
                   | ((hash[offset + 2] & 0xff) << 8)
                   | (hash[offset + 3] & 0xff);
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] FromBase32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        input = input.Trim().TrimEnd('=').ToUpperInvariant().Replace(" ", string.Empty);

        var bits = 0;
        var value = 0;
        var output = new List<byte>();
        foreach (var c in input)
        {
            var idx = alphabet.IndexOf(c);
            if (idx < 0) continue;
            value = (value << 5) | idx;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xff));
                bits -= 8;
            }
        }
        return [.. output];
    }
}
