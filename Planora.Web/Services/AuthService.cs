using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using Planora.Shared.DTOs.Auth;
using Planora.Web.Auth;

namespace Planora.Web.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;
    private readonly PlanorAuthStateProvider _authState;

    public AuthService(HttpClient http, ILocalStorageService localStorage, PlanorAuthStateProvider authState)
    {
        _http = http;
        _localStorage = localStorage;
        _authState = authState;
    }

    public async Task<(bool Success, bool RequiresTwoFactor, string? Error)> LoginAsync(LoginRequest request)
    {
        HttpResponseMessage res;
        try { res = await _http.PostAsJsonAsync("api/auth/login", request); }
        catch { return (false, false, "Couldn't reach the server. Please try again."); }

        if (!res.IsSuccessStatusCode) return (false, false, "Invalid email or password.");

        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        if (auth is { RequiresTwoFactor: true })
            return (false, true, null);

        var (ok, err) = await StoreAuthAsync(auth);
        return (ok, false, err);
    }

    // Second step of login for accounts with 2FA enabled. The password is re-verified server-side.
    public async Task<(bool Success, string? Error)> LoginTwoFactorAsync(string email, string password, string code, bool isRecoveryCode)
    {
        HttpResponseMessage res;
        try
        {
            res = await _http.PostAsJsonAsync("api/auth/login/2fa", new TwoFactorLoginRequest
            {
                Email = email,
                Password = password,
                Code = code,
                IsRecoveryCode = isRecoveryCode
            });
        }
        catch { return (false, "Couldn't reach the server. Please try again."); }

        if (!res.IsSuccessStatusCode)
            return (false, res.StatusCode == HttpStatusCode.TooManyRequests
                ? "Too many attempts. Please wait a minute and try again."
                : "That code is invalid or expired. Please try again.");

        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        return await StoreAuthAsync(auth);
    }

    public async Task<(TwoFactorStatusResponse? Status, string? Error)> Get2faStatusAsync()
    {
        try
        {
            var res = await _http.GetAsync("api/auth/2fa/status");
            if (!res.IsSuccessStatusCode) return (null, await ReadErrorAsync(res, "Could not load 2FA status."));
            return (await res.Content.ReadFromJsonAsync<TwoFactorStatusResponse>(), null);
        }
        catch { return (null, "Couldn't reach the server. Please try again."); }
    }

    public async Task<(TwoFactorSetupResponse? Setup, string? Error)> Setup2faAsync()
    {
        try
        {
            var res = await _http.PostAsync("api/auth/2fa/setup", null);
            if (!res.IsSuccessStatusCode) return (null, await ReadErrorAsync(res, "Could not start 2FA setup."));
            return (await res.Content.ReadFromJsonAsync<TwoFactorSetupResponse>(), null);
        }
        catch { return (null, "Couldn't reach the server. Please try again."); }
    }

    public async Task<(List<string>? RecoveryCodes, string? Error)> Enable2faAsync(string code)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/auth/2fa/enable", new EnableTwoFactorRequest { Code = code });
            if (!res.IsSuccessStatusCode) return (null, await ReadErrorAsync(res, "Could not enable 2FA."));
            var body = await res.Content.ReadFromJsonAsync<TwoFactorRecoveryCodesResponse>();
            return (body?.RecoveryCodes ?? [], null);
        }
        catch { return (null, "Couldn't reach the server. Please try again."); }
    }

    public async Task<(bool Success, string? Error)> Disable2faAsync(string code, bool isRecoveryCode)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/auth/2fa/disable",
                new DisableTwoFactorRequest { Code = code, IsRecoveryCode = isRecoveryCode });
            if (res.IsSuccessStatusCode) return (true, null);
            return (false, await ReadErrorAsync(res, "Could not disable 2FA."));
        }
        catch { return (false, "Couldn't reach the server. Please try again."); }
    }

    public async Task<(List<string>? RecoveryCodes, string? Error)> RegenerateRecoveryCodesAsync(string code, bool isRecoveryCode)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/auth/2fa/recovery-codes",
                new DisableTwoFactorRequest { Code = code, IsRecoveryCode = isRecoveryCode });
            if (!res.IsSuccessStatusCode) return (null, await ReadErrorAsync(res, "Could not regenerate recovery codes."));
            var body = await res.Content.ReadFromJsonAsync<TwoFactorRecoveryCodesResponse>();
            return (body?.RecoveryCodes ?? [], null);
        }
        catch { return (null, "Couldn't reach the server. Please try again."); }
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(RegisterRequest request)
    {
        HttpResponseMessage res;
        try { res = await _http.PostAsJsonAsync("api/auth/register", request); }
        catch { return (false, "Couldn't reach the server. Please try again."); }

        if (!res.IsSuccessStatusCode)
        {
            try
            {
                var errors = await res.Content.ReadFromJsonAsync<List<string>>();
                return (false, errors?.FirstOrDefault() ?? "Registration failed.");
            }
            catch
            {
                return (false, $"Server error ({(int)res.StatusCode}). Check that the database is running.");
            }
        }

        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        return await StoreAuthAsync(auth);
    }

    public async Task<(bool Success, string? Error)> DemoLoginAsync()
    {
        HttpResponseMessage res;
        try { res = await _http.PostAsync("api/auth/demo", null); }
        catch { return (false, "Couldn't reach the server. Please try again."); }

        if (!res.IsSuccessStatusCode)
            return (false, "Could not create demo account. Please try again.");

        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        return await StoreAuthAsync(auth);
    }

    public async Task<(bool Success, string? Error)> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        HttpResponseMessage res;
        try { res = await _http.PostAsJsonAsync("api/auth/forgot-password", request); }
        catch { return (false, "Couldn't reach the server. Please try again."); }

        if (res.IsSuccessStatusCode) return (true, null);

        return (false, await ReadErrorAsync(res, "Could not send a reset link. Please try again."));
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(ResetPasswordRequest request)
    {
        HttpResponseMessage res;
        try { res = await _http.PostAsJsonAsync("api/auth/reset-password", request); }
        catch { return (false, "Couldn't reach the server. Please try again."); }

        if (res.IsSuccessStatusCode) return (true, null);

        return (false, await ReadErrorAsync(res, "Could not reset your password."));
    }

    public async Task<(bool Success, string? Error)> SendEmailConfirmationAsync()
    {
        HttpResponseMessage res;
        try { res = await _http.PostAsync("api/auth/send-email-confirmation", null); }
        catch { return (false, "Couldn't reach the server. Please try again."); }

        if (res.IsSuccessStatusCode) return (true, null);

        return (false, await ReadErrorAsync(res, "Could not send a verification email."));
    }

    public async Task<(bool Success, string? Error)> ConfirmEmailAsync(ConfirmEmailRequest request)
    {
        HttpResponseMessage res;
        try { res = await _http.PostAsJsonAsync("api/auth/confirm-email", request); }
        catch { return (false, "Couldn't reach the server. Please try again."); }

        if (res.IsSuccessStatusCode) return (true, null);

        return (false, await ReadErrorAsync(res, "Could not verify your email."));
    }

    public async Task<(List<RefreshSessionDto> Sessions, string? Error)> GetSessionsAsync()
    {
        var refreshToken = await _localStorage.GetItemAsync<string>("refreshToken") ?? "";
        HttpResponseMessage res;
        try
        {
            res = await _http.PostAsJsonAsync("api/auth/sessions", new SessionListRequest
            {
                CurrentRefreshToken = refreshToken
            });
        }
        catch
        {
            return ([], "Couldn't reach the server. Please try again.");
        }

        if (!res.IsSuccessStatusCode)
            return ([], await ReadErrorAsync(res, "Could not load sessions."));

        return (await res.Content.ReadFromJsonAsync<List<RefreshSessionDto>>() ?? [], null);
    }

    public async Task<(bool Success, string? Error)> RevokeSessionAsync(Guid sessionId)
    {
        HttpResponseMessage res;
        try
        {
            res = await _http.PostAsJsonAsync("api/auth/sessions/revoke", new RevokeSessionRequest
            {
                SessionId = sessionId
            });
        }
        catch
        {
            return (false, "Couldn't reach the server. Please try again.");
        }

        if (res.IsSuccessStatusCode) return (true, null);

        return (false, await ReadErrorAsync(res, "Could not revoke that session."));
    }

    public async Task<(bool Success, string? Error)> RevokeOtherSessionsAsync()
    {
        var refreshToken = await _localStorage.GetItemAsync<string>("refreshToken") ?? "";
        if (string.IsNullOrWhiteSpace(refreshToken))
            return (false, "Current session is missing. Please sign in again.");

        HttpResponseMessage res;
        try
        {
            res = await _http.PostAsJsonAsync("api/auth/sessions/revoke-others", new RevokeOtherSessionsRequest
            {
                CurrentRefreshToken = refreshToken
            });
        }
        catch
        {
            return (false, "Couldn't reach the server. Please try again.");
        }

        if (res.IsSuccessStatusCode) return (true, null);

        return (false, await ReadErrorAsync(res, "Could not revoke other sessions."));
    }

    public async Task LogoutAsync()
    {
        var refreshToken = await _localStorage.GetItemAsync<string>("refreshToken");
        try { await _http.PostAsJsonAsync("api/auth/logout", new LogoutRequest(refreshToken)); } catch { }
        await _localStorage.RemoveItemAsync("authToken");
        await _localStorage.RemoveItemAsync("refreshToken");
        _authState.NotifyLoggedOut();
    }

    private async Task<(bool Success, string? Error)> StoreAuthAsync(AuthResponse? auth)
    {
        if (auth is null)
            return (false, "The server returned an invalid sign-in response.");

        try
        {
            await _localStorage.SetItemAsync("authToken", auth.Token);
            await _localStorage.SetItemAsync("refreshToken", auth.RefreshToken);
            _authState.NotifyLoggedIn(auth.Token);
            return (true, null);
        }
        catch
        {
            return (false, "Could not save your sign-in session. Please try again.");
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage res, string fallback)
    {
        if (res.StatusCode == HttpStatusCode.TooManyRequests)
            return "Too many attempts. Please wait a minute and try again.";

        var body = await res.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
            return fallback;

        try
        {
            var errors = JsonSerializer.Deserialize<List<string>>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var first = errors?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
                return first;
        }
        catch (JsonException) { }

        try
        {
            var message = JsonSerializer.Deserialize<string>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (!string.IsNullOrWhiteSpace(message))
                return message;
        }
        catch (JsonException) { }

        return fallback;
    }
}
