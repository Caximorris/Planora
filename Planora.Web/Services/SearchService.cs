using System.Net.Http.Json;
using Planora.Shared.DTOs.Search;

namespace Planora.Web.Services;

public class SearchService
{
    private readonly HttpClient _http;
    public SearchService(HttpClient http) => _http = http;

    public Task<List<SearchResultDto>?> SearchAsync(string query) =>
        _http.GetFromJsonAsync<List<SearchResultDto>>($"api/search?q={Uri.EscapeDataString(query)}");
}
