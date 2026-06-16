using System.Net.Http.Json;
using Planora.Shared.DTOs.Card;

namespace Planora.Web.Services;

public class CardService
{
    private readonly HttpClient _http;
    public CardService(HttpClient http) => _http = http;

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

    public async Task<bool> DeleteAsync(Guid id) =>
        (await _http.DeleteAsync($"api/cards/{id}")).IsSuccessStatusCode;
}
