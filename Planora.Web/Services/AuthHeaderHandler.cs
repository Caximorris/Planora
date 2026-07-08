using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Planora.Shared.DTOs.Auth;
using Planora.Web.Auth;

namespace Planora.Web.Services;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;
    private readonly NavigationManager _navigation;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PlanorAuthStateProvider _authState;
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthHeaderHandler(
        ILocalStorageService localStorage,
        NavigationManager navigation,
        IHttpClientFactory httpClientFactory,
        PlanorAuthStateProvider authState)
    {
        _localStorage = localStorage;
        _navigation = navigation;
        _httpClientFactory = httpClientFactory;
        _authState = authState;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isAnonymousAuthEndpoint = IsAnonymousAuthEndpoint(request);
        if (isAnonymousAuthEndpoint)
        {
            request.Headers.Authorization = null;
        }
        else
        {
            var token = await _localStorage.GetItemAsync<string>("authToken");
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            else
                request.Headers.Authorization = null;
        }

        // Buffer the body up-front so the request can be safely replayed after a silent refresh.
        byte[]? bufferedBody = null;
        if (!isAnonymousAuthEndpoint && request.Content is not null)
            bufferedBody = await request.Content.ReadAsByteArrayAsync(cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);

        // On 401 from a non-auth endpoint, attempt a silent token refresh. A 401 means the token was
        // rejected in OnTokenValidated *before* the action ran (e.g. the SecurityStamp rotated after
        // enabling 2FA), so replaying the request once with the fresh token is safe and transparent.
        if (response.StatusCode == HttpStatusCode.Unauthorized
            && !isAnonymousAuthEndpoint)
        {
            var newToken = await TrySilentRefreshAsync(cancellationToken);
            if (newToken is not null)
            {
                var retry = CloneRequest(request, bufferedBody);
                retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
                response.Dispose();
                response = await base.SendAsync(retry, cancellationToken);
            }
            else
            {
                await _localStorage.RemoveItemAsync("authToken");
                await _localStorage.RemoveItemAsync("refreshToken");
                _authState.NotifyLoggedOut();
                _navigation.NavigateTo("/login", forceLoad: false);
            }
        }

        return response;
    }

    // Rebuilds a request so it can be re-sent after a token refresh (an HttpRequestMessage and its
    // content stream can each only be sent once).
    private static HttpRequestMessage CloneRequest(HttpRequestMessage request, byte[]? body)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };

        if (body is not null)
        {
            var content = new ByteArrayContent(body);
            if (request.Content?.Headers is not null)
                foreach (var header in request.Content.Headers)
                    content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            clone.Content = content;
        }

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }

    private static bool IsAnonymousAuthEndpoint(HttpRequestMessage request)
    {
        var uri = request.RequestUri;
        var path = uri is null
            ? ""
            : uri.IsAbsoluteUri
                ? uri.AbsolutePath
                : "/" + uri.OriginalString.TrimStart('/').Split('?', 2)[0];

        return path.Equals("/api/auth/register", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/refresh", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/demo", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/forgot-password", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/reset-password", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/confirm-email", StringComparison.OrdinalIgnoreCase);
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
            _authState.NotifyLoggedIn(auth.Token);
            return auth.Token;
        }
        catch { return null; }
        finally { _refreshLock.Release(); }
    }
}
