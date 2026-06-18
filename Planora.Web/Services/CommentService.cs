using System.Net.Http.Json;
using Planora.Shared.DTOs.Card;

namespace Planora.Web.Services;

public class CommentService
{
    private readonly HttpClient _http;
    public CommentService(HttpClient http) => _http = http;

    public Task<List<CardCommentDto>?> GetAllAsync(Guid cardId) =>
        _http.GetFromJsonAsync<List<CardCommentDto>>($"api/cards/{cardId}/comments");

    public async Task<CardCommentDto?> CreateAsync(Guid cardId, string text)
    {
        var res = await _http.PostAsJsonAsync($"api/cards/{cardId}/comments", new CreateCommentRequest { Text = text });
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<CardCommentDto>() : null;
    }

    public async Task<bool> DeleteAsync(Guid cardId, Guid commentId) =>
        (await _http.DeleteAsync($"api/cards/{cardId}/comments/{commentId}")).IsSuccessStatusCode;
}
