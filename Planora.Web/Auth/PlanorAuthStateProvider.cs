using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace Planora.Web.Auth;

public class PlanorAuthStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly HttpClient _http;
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public PlanorAuthStateProvider(ILocalStorageService localStorage, HttpClient http)
    {
        _localStorage = localStorage;
        _http = http;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (string.IsNullOrWhiteSpace(token)) return Anonymous;

        if (IsExpired(token))
        {
            await _localStorage.RemoveItemAsync("authToken");
            return Anonymous;
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
