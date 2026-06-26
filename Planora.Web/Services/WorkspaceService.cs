using System.Net.Http.Json;
using Planora.Shared.DTOs.Board;
using Planora.Shared.DTOs.Calendar;
using Planora.Shared.DTOs.Invitation;
using Planora.Shared.DTOs.Workspace;
using Planora.Shared.Enums;

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

    public Task<List<WorkspaceMemberDto>?> GetMembersAsync(Guid workspaceId) =>
        _http.GetFromJsonAsync<List<WorkspaceMemberDto>>($"api/workspaces/{workspaceId}/members");

    public async Task<WorkspaceDto?> CreateAsync(CreateWorkspaceRequest request)
    {
        var res = await _http.PostAsJsonAsync("api/workspaces", request);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<WorkspaceDto>() : null;
    }

    public async Task<bool> DeleteAsync(Guid id) =>
        (await _http.DeleteAsync($"api/workspaces/{id}")).IsSuccessStatusCode;

    public async Task<bool> RemoveMemberAsync(Guid workspaceId, string userId) =>
        (await _http.DeleteAsync($"api/workspaces/{workspaceId}/members/{userId}")).IsSuccessStatusCode;

    public async Task<bool> UpdateMemberRoleAsync(Guid workspaceId, string userId, WorkspaceRole role)
    {
        var res = await _http.PatchAsJsonAsync($"api/workspaces/{workspaceId}/members/{userId}", new UpdateMemberRoleRequest { Role = role });
        return res.IsSuccessStatusCode;
    }

    public async Task<InvitationDto?> CreateInvitationAsync(Guid workspaceId, CreateInvitationRequest request)
    {
        var res = await _http.PostAsJsonAsync($"api/workspaces/{workspaceId}/invitations", request);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<InvitationDto>() : null;
    }

    public Task<List<CalendarCardDto>?> GetCalendarAsync(Guid workspaceId, string month) =>
        _http.GetFromJsonAsync<List<CalendarCardDto>>($"api/workspaces/{workspaceId}/calendar?month={month}");
}
