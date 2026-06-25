using System.Net.Http.Json;
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

    public async Task<(bool Success, string? Error)> LoginAsync(LoginRequest request)
    {
        var res = await _http.PostAsJsonAsync("api/auth/login", request);
        if (!res.IsSuccessStatusCode) return (false, "Invalid email or password.");

        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        await _localStorage.SetItemAsync("authToken", auth!.Token);
        await _localStorage.SetItemAsync("refreshToken", auth.RefreshToken);
        _authState.NotifyLoggedIn(auth.Token);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(RegisterRequest request)
    {
        var res = await _http.PostAsJsonAsync("api/auth/register", request);
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
        await _localStorage.SetItemAsync("authToken", auth!.Token);
        await _localStorage.SetItemAsync("refreshToken", auth.RefreshToken);
        _authState.NotifyLoggedIn(auth.Token);
        return (true, null);
    }

    public async Task LogoutAsync()
    {
        var refreshToken = await _localStorage.GetItemAsync<string>("refreshToken");
        try { await _http.PostAsJsonAsync("api/auth/logout", new LogoutRequest(refreshToken)); } catch { }
        await _localStorage.RemoveItemAsync("authToken");
        await _localStorage.RemoveItemAsync("refreshToken");
        _authState.NotifyLoggedOut();
    }
}
