using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Planora.Shared.DTOs.Auth;

namespace Planora.Web.Auth;

public class PlanorAuthStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly HttpClient _http;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public PlanorAuthStateProvider(
        ILocalStorageService localStorage,
        HttpClient http,
        IHttpClientFactory httpClientFactory)
    {
        _localStorage = localStorage;
        _http = http;
        _httpClientFactory = httpClientFactory;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (string.IsNullOrWhiteSpace(token)) return Anonymous;

        // Proactive silent refresh when the access token has expired
        if (IsExpired(token))
        {
            token = await TrySilentRefreshAsync();
            if (token is null)
            {
                await _localStorage.RemoveItemAsync("authToken");
                await _localStorage.RemoveItemAsync("refreshToken");
                return Anonymous;
            }
        }

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return BuildState(token);
    }

    public void NotifyLoggedIn(string token)
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        NotifyAuthenticationStateChanged(Task.FromResult(BuildState(token)));
    }

    public void NotifyLoggedOut()
    {
        _http.DefaultRequestHeaders.Authorization = null;
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    private async Task<string?> TrySilentRefreshAsync()
    {
        try
        {
            var refreshToken = await _localStorage.GetItemAsync<string>("refreshToken");
            if (string.IsNullOrEmpty(refreshToken)) return null;

            // Use the bare client (no auth handler) to avoid circular dependency
            var client = _httpClientFactory.CreateClient("PlanoraRefresh");
            var res = await client.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(refreshToken));
            if (!res.IsSuccessStatusCode) return null;

            var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null) return null;

            await _localStorage.SetItemAsync("authToken", auth.Token);
            await _localStorage.SetItemAsync("refreshToken", auth.RefreshToken);
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
            return auth.Token;
        }
        catch { return null; }
    }

    private static AuthenticationState BuildState(string token)
    {
        var claims = ParseClaims(token);
        var identity = new ClaimsIdentity(claims, "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private static bool IsExpired(string token)
    {
        var claims = ParseClaims(token);
        var exp = claims.FirstOrDefault(c => c.Type == "exp")?.Value;
        if (exp is null) return false;
        return DateTimeOffset.FromUnixTimeSeconds(long.Parse(exp)) < DateTimeOffset.UtcNow;
    }

    private static IEnumerable<Claim> ParseClaims(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded = (payload.Length % 4) switch
        {
            2 => payload + "==",
            3 => payload + "=",
            _ => payload
        };
        var json = Convert.FromBase64String(padded);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return dict.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString()!));
    }
}
