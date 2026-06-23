using System.Net;
using System.Net.Http.Headers;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;

namespace Planora.Web.Services;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;
    private readonly NavigationManager _navigation;

    public AuthHeaderHandler(ILocalStorageService localStorage, NavigationManager navigation)
    {
        _localStorage = localStorage;
        _navigation = navigation;
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

        // Token revocado o expirado en el servidor — limpiar sesión y redirigir al login
        if (response.StatusCode == HttpStatusCode.Unauthorized
            && request.RequestUri?.PathAndQuery.Contains("/api/auth/") == false)
        {
            await _localStorage.RemoveItemAsync("authToken");
            _navigation.NavigateTo("/login", forceLoad: false);
        }

        return response;
    }
}
