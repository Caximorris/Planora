using System.Net.Http.Json;
using Planora.Shared.DTOs.Invitation;

namespace Planora.Web.Services;

public class InvitationService
{
    private readonly HttpClient _http;
    public InvitationService(HttpClient http) => _http = http;

    public Task<InvitationDto?> GetByTokenAsync(string token) =>
        _http.GetFromJsonAsync<InvitationDto>($"api/invitations/{token}");

    public async Task<(bool Success, Guid WorkspaceId)> AcceptAsync(string token)
    {
        var res = await _http.PostAsync($"api/invitations/{token}/accept", null);
        if (!res.IsSuccessStatusCode) return (false, Guid.Empty);
        var body = await res.Content.ReadFromJsonAsync<AcceptResult>();
        return (true, body?.WorkspaceId ?? Guid.Empty);
    }

    public async Task<bool> DeclineAsync(string token) =>
        (await _http.PostAsync($"api/invitations/{token}/decline", null)).IsSuccessStatusCode;

    private record AcceptResult(Guid WorkspaceId);
}
