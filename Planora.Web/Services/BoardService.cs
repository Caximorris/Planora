using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Planora.Shared.Constants;
using Planora.Shared.DTOs.Activity;
using Planora.Shared.DTOs.Board;

namespace Planora.Web.Services;

public class BoardService
{
    private readonly HttpClient _http;
    public BoardService(HttpClient http) => _http = http;

    public Task<BoardDetailDto?> GetByIdAsync(Guid id, bool includeArchived = false) =>
        _http.GetFromJsonAsync<BoardDetailDto>($"api/boards/{id}?includeArchived={includeArchived}");

    public async Task<BoardDto?> CreateAsync(CreateBoardRequest request)
    {
        var res = await _http.PostAsJsonAsync("api/boards", request);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<BoardDto>() : null;
    }

    public async Task<BoardDto?> UpdateAsync(Guid id, UpdateBoardRequest request)
    {
        var res = await _http.PutAsJsonAsync($"api/boards/{id}", request);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<BoardDto>() : null;
    }

    public Task<List<BoardDto>?> GetByWorkspaceAsync(Guid workspaceId, bool includeArchived = false) =>
        _http.GetFromJsonAsync<List<BoardDto>>($"api/workspaces/{workspaceId}/boards?includeArchived={includeArchived}");

    public Task<List<ActivityEventDto>?> GetActivityAsync(Guid boardId, int take = 30) =>
        _http.GetFromJsonAsync<List<ActivityEventDto>>($"api/boards/{boardId}/activity?take={take}");

    public async Task<BoardDto?> ArchiveAsync(Guid id)
    {
        var res = await _http.PatchAsync($"api/boards/{id}/archive", null);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<BoardDto>() : null;
    }

    public async Task<BoardDto?> UnarchiveAsync(Guid id)
    {
        var res = await _http.PatchAsync($"api/boards/{id}/unarchive", null);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<BoardDto>() : null;
    }

    public async Task<bool> DeleteAsync(Guid id) =>
        (await _http.DeleteAsync($"api/boards/{id}")).IsSuccessStatusCode;

    public async Task<BoardDto?> UploadCoverImageAsync(Guid boardId, IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(BoardLimits.MaxCoverImageBytes);
        using var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(streamContent, "file", file.Name);

        var res = await _http.PostAsync($"api/boards/{boardId}/cover-image", content);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<BoardDto>() : null;
    }

    public async Task<bool> RemoveCoverImageAsync(Guid boardId) =>
        (await _http.DeleteAsync($"api/boards/{boardId}/cover-image")).IsSuccessStatusCode;

    /// Converts the API's relative cover-image path (e.g. "/uploads/boards/x.png") into an
    /// absolute URL against the API origin, since the Blazor app is served from a different origin.
    public string? ResolveImageUrl(string? relativeUrl) =>
        string.IsNullOrWhiteSpace(relativeUrl) ? null : new Uri(_http.BaseAddress!, relativeUrl).ToString();

    /// Shared CSS `style` value for anything that shows a board's cover (board tiles, board
    /// header): cover image takes priority over the plain color. Single source of truth so the
    /// board page and the workspace tiles can't drift apart on how a cover is rendered.
    public string ResolveBackgroundStyle(string? coverImageUrl, string? coverColor, string fallbackColor = "")
    {
        var resolvedImage = ResolveImageUrl(coverImageUrl);
        if (resolvedImage is not null)
            return $"background-image:url('{resolvedImage}');background-size:cover;background-position:center;";

        var color = PlanoraColors.SafeBoardBackgroundOrNull(coverColor)
            ?? PlanoraColors.SafeBoardBackgroundOrNull(fallbackColor);
        return color is not null ? $"background:{color};" : "";
    }
}
