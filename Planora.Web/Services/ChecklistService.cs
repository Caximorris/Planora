using System.Net.Http.Json;
using Planora.Shared.DTOs.Checklist;

namespace Planora.Web.Services;

public class ChecklistService
{
    private readonly HttpClient _http;
    public ChecklistService(HttpClient http) => _http = http;

    public async Task<ChecklistDto?> CreateAsync(Guid cardId, string title)
    {
        var res = await _http.PostAsJsonAsync("api/checklists", new CreateChecklistRequest { CardId = cardId, Title = title });
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<ChecklistDto>() : null;
    }

    public async Task<ChecklistDto?> UpdateTitleAsync(Guid id, string title)
    {
        var res = await _http.PutAsJsonAsync($"api/checklists/{id}", new UpdateChecklistRequest { Title = title });
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<ChecklistDto>() : null;
    }

    public async Task<bool> DeleteAsync(Guid id) =>
        (await _http.DeleteAsync($"api/checklists/{id}")).IsSuccessStatusCode;

    public async Task<ChecklistItemDto?> AddItemAsync(Guid checklistId, string text)
    {
        var res = await _http.PostAsJsonAsync($"api/checklists/{checklistId}/items", new CreateChecklistItemRequest { Text = text });
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<ChecklistItemDto>() : null;
    }

    public async Task<ChecklistItemDto?> UpdateItemAsync(Guid itemId, UpdateChecklistItemRequest request)
    {
        var res = await _http.PutAsJsonAsync($"api/checklists/items/{itemId}", request);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<ChecklistItemDto>() : null;
    }

    public async Task<bool> DeleteItemAsync(Guid itemId) =>
        (await _http.DeleteAsync($"api/checklists/items/{itemId}")).IsSuccessStatusCode;
}
