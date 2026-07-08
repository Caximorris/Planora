using System.Net.Http.Json;
using Planora.Shared.DTOs.Card;

namespace Planora.Web.Services;

public class CardService
{
    private readonly HttpClient _http;
    public CardService(HttpClient http) => _http = http;

    public Task<CardDto?> GetByIdAsync(Guid id) =>
        _http.GetFromJsonAsync<CardDto>($"api/cards/{id}");

    public async Task<CardDto?> CreateAsync(CreateCardRequest request)
    {
        var res = await _http.PostAsJsonAsync("api/cards", request);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<CardDto>() : null;
    }

    public async Task<CardDto?> UpdateAsync(Guid id, UpdateCardRequest request)
    {
        var res = await _http.PutAsJsonAsync($"api/cards/{id}", request);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<CardDto>() : null;
    }

    public async Task<CardDto?> ArchiveAsync(Guid id)
    {
        var res = await _http.PatchAsync($"api/cards/{id}/archive", null);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<CardDto>() : null;
    }

    public async Task<CardDto?> UnarchiveAsync(Guid id)
    {
        var res = await _http.PatchAsync($"api/cards/{id}/unarchive", null);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<CardDto>() : null;
    }

    /// Soft-delete: moves the card to its board's trash (recoverable).
    public async Task<bool> DeleteAsync(Guid id) =>
        (await _http.DeleteAsync($"api/cards/{id}")).IsSuccessStatusCode;

    public Task<List<CardDto>?> GetTrashAsync(Guid boardId) =>
        _http.GetFromJsonAsync<List<CardDto>>($"api/cards/trash?boardId={boardId}");

    public async Task<CardDto?> RestoreAsync(Guid id)
    {
        var res = await _http.PatchAsync($"api/cards/{id}/restore", null);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<CardDto>() : null;
    }

    /// Permanent (irreversible) delete — only valid for a card already in the trash.
    public async Task<bool> DeletePermanentAsync(Guid id) =>
        (await _http.DeleteAsync($"api/cards/{id}/permanent")).IsSuccessStatusCode;
}
