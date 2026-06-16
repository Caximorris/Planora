using System.Net.Http.Json;
using Planora.Shared.DTOs.Column;

namespace Planora.Web.Services;

public class ColumnService
{
    private readonly HttpClient _http;
    public ColumnService(HttpClient http) => _http = http;

    public async Task<ColumnDto?> CreateAsync(CreateColumnRequest request)
    {
        var res = await _http.PostAsJsonAsync("api/columns", request);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<ColumnDto>() : null;
    }

    public async Task<ColumnDto?> UpdateAsync(Guid id, UpdateColumnRequest request)
    {
        var res = await _http.PutAsJsonAsync($"api/columns/{id}", request);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<ColumnDto>() : null;
    }

    public async Task<bool> DeleteAsync(Guid id) =>
        (await _http.DeleteAsync($"api/columns/{id}")).IsSuccessStatusCode;
}
