using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Planora.Shared.DTOs.Auth;

namespace Planora.Web.Services;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;
    private readonly NavigationManager _navigation;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthHeaderHandler(
        ILocalStorageService localStorage,
        NavigationManager navigation,
        IHttpClientFactory httpClientFactory)
    {
        _localStorage = localStorage;
        _navigation = navigation;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        else
            request.Headers.Authorization = null;

        var response = await base.SendAsync(request, cancellationToken);

        // On 401 from a non-auth endpoint, attempt a silent token refresh
        if (response.StatusCode == HttpStatusCode.Unauthorized
            && request.RequestUri?.PathAndQuery.Contains("/api/auth/") == false)
        {
            var newToken = await TrySilentRefreshAsync(cancellationToken);
            if (newToken is null)
            {
                await _localStorage.RemoveItemAsync("authToken");
                await _localStorage.RemoveItemAsync("refreshToken");
                _navigation.NavigateTo("/login", forceLoad: false);
            }
        }

        return response;
    }

    private async Task<string?> TrySilentRefreshAsync(CancellationToken ct)
    {
        if (!await _refreshLock.WaitAsync(5000, ct)) return null;
        try
        {
            var refreshToken = await _localStorage.GetItemAsync<string>("refreshToken");
            if (string.IsNullOrEmpty(refreshToken)) return null;

            // Use the bare client (no auth handler) to avoid circular calls
            var client = _httpClientFactory.CreateClient("PlanoraRefresh");
            var res = await client.PostAsJsonAsync("api/auth/refresh",
                new RefreshRequest(refreshToken), ct);
            if (!res.IsSuccessStatusCode) return null;

            var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
            if (auth is null) return null;

            await _localStorage.SetItemAsync("authToken", auth.Token);
            await _localStorage.SetItemAsync("refreshToken", auth.RefreshToken);
            return auth.Token;
        }
        catch { return null; }
        finally { _refreshLock.Release(); }
    }
}
