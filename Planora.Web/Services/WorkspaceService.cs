using System.Net.Http.Json;
using Planora.Shared.DTOs.Board;
using Planora.Shared.DTOs.Workspace;

namespace Planora.Web.Services;

public class WorkspaceService
{
    private readonly HttpClient _http;
    public WorkspaceService(HttpClient http) => _http = http;

    public Task<List<WorkspaceDto>?> GetAllAsync() =>
        _http.GetFromJsonAsync<List<WorkspaceDto>>("api/workspaces");

    public Task<WorkspaceDto?> GetByIdAsync(Guid id) =>
        _http.GetFromJsonAsync<WorkspaceDto>($"api/workspaces/{id}");

    public Task<List<BoardDto>?> GetBoardsAsync(Guid workspaceId) =>
        _http.GetFromJsonAsync<List<BoardDto>>($"api/workspaces/{workspaceId}/boards");

    public async Task<WorkspaceDto?> CreateAsync(CreateWorkspaceRequest request)
    {
        var res = await _http.PostAsJsonAsync("api/workspaces", request);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<WorkspaceDto>() : null;
    }

    public async Task<bool> DeleteAsync(Guid id) =>
        (await _http.DeleteAsync($"api/workspaces/{id}")).IsSuccessStatusCode;
}
