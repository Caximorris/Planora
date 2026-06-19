using System.Net.Http.Json;
using Planora.Shared.DTOs.Label;

namespace Planora.Web.Services;

public class LabelService
{
    private readonly HttpClient _http;
    public LabelService(HttpClient http) => _http = http;

    public Task<List<LabelDto>?> GetWorkspaceLabelsAsync(Guid workspaceId) =>
        _http.GetFromJsonAsync<List<LabelDto>>($"api/labels/workspace/{workspaceId}");

    public async Task<LabelDto?> CreateAsync(Guid workspaceId, CreateLabelRequest request)
    {
        var res = await _http.PostAsJsonAsync($"api/labels/workspace/{workspaceId}", request);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<LabelDto>() : null;
    }

    public async Task<LabelDto?> UpdateAsync(Guid id, UpdateLabelRequest request)
    {
        var res = await _http.PutAsJsonAsync($"api/labels/{id}", request);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<LabelDto>() : null;
    }

    public async Task<bool> DeleteAsync(Guid id) =>
        (await _http.DeleteAsync($"api/labels/{id}")).IsSuccessStatusCode;

    public async Task<bool> AttachToCardAsync(Guid labelId, Guid cardId) =>
        (await _http.PostAsync($"api/labels/{labelId}/cards/{cardId}", null)).IsSuccessStatusCode;

    public async Task<bool> DetachFromCardAsync(Guid labelId, Guid cardId) =>
        (await _http.DeleteAsync($"api/labels/{labelId}/cards/{cardId}")).IsSuccessStatusCode;
}
