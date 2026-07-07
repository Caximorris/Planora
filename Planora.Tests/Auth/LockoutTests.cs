using System.Net;
using System.Net.Http.Json;
using Planora.Shared.DTOs.Auth;
using Planora.Tests.Infrastructure;

namespace Planora.Tests.Auth;

/// <summary>
/// Progressive account lockout (manual, in AuthController.Login). Failed attempts
/// accumulate on AccessFailedCount; at the third failure a lockout window is set,
/// after which further attempts — even with the correct password — are blocked with
/// 429 until the window elapses. Thresholds live in code, not Identity's built-in lockout.
/// </summary>
[Collection("Integration")]
public class LockoutTests(PlanoraWebAppFactory factory)
{
    private static LoginRequest WrongLogin(string email) => new() { Email = email, Password = "Wrong9999" };

    [Fact]
    public async Task Fourth_failed_attempt_is_locked_out()
    {
        var client = factory.CreateClient();
        var user = AuthTestHelpers.NewUser();
        (await client.PostAsJsonAsync("/api/auth/register", user)).EnsureSuccessStatusCode();

        // Attempts 1-3: wrong password → 401 Unauthorized (lockout window is set on the 3rd).
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var failed = await client.PostAsJsonAsync("/api/auth/login", WrongLogin(user.Email));
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        // Attempt 4 is rejected before the password is even checked → 429.
        var locked = await client.PostAsJsonAsync("/api/auth/login", WrongLogin(user.Email));
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
    }

    [Fact]
    public async Task Lockout_blocks_even_the_correct_password()
    {
        var client = factory.CreateClient();
        var user = AuthTestHelpers.NewUser();
        (await client.PostAsJsonAsync("/api/auth/register", user)).EnsureSuccessStatusCode();

        for (var attempt = 1; attempt <= 3; attempt++)
            await client.PostAsJsonAsync("/api/auth/login", WrongLogin(user.Email));

        // Correct credentials, but the account is locked → still 429, not 200.
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = user.Email, Password = user.Password });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Successful_login_resets_the_failure_counter()
    {
        var client = factory.CreateClient();
        var user = AuthTestHelpers.NewUser();
        (await client.PostAsJsonAsync("/api/auth/register", user)).EnsureSuccessStatusCode();

        // Two failures (below the 3-attempt lockout threshold)...
        await client.PostAsJsonAsync("/api/auth/login", WrongLogin(user.Email));
        await client.PostAsJsonAsync("/api/auth/login", WrongLogin(user.Email));

        // ...then a success, which resets the counter.
        var ok = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = user.Email, Password = user.Password });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        // A single subsequent failure must not be locked out (counter was reset).
        var failed = await client.PostAsJsonAsync("/api/auth/login", WrongLogin(user.Email));
        Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
    }
}
