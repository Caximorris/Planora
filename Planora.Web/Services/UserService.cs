using System.Net;
using System.Net.Http.Json;
using Planora.Shared.DTOs.Account;
using Planora.Shared.DTOs.Users;

namespace Planora.Web.Services;

public class UserService
{
    private readonly HttpClient _http;
    public UserService(HttpClient http) => _http = http;

    public async Task<(UserProfileDto? Profile, string? Error)> GetProfileAsync()
    {
        var res = await _http.GetAsync("api/users/profile");
        if (res.IsSuccessStatusCode)
            return (await res.Content.ReadFromJsonAsync<UserProfileDto>(), null);

        var body = await res.Content.ReadAsStringAsync();
        return (null, string.IsNullOrWhiteSpace(body) ? "Could not load profile." : body.Trim('"'));
    }

    public async Task<(bool Success, string? Error)> UpdateProfileAsync(UpdateProfileRequest request)
    {
        var res = await _http.PutAsJsonAsync("api/users/profile", request);
        if (res.IsSuccessStatusCode) return (true, null);
        var body = await res.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? "Could not update profile." : body.Trim('"'));
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var res = await _http.PostAsJsonAsync("api/users/change-password", request);
        if (res.IsSuccessStatusCode) return (true, null);
        var body = await res.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? "Could not change password." : body.Trim('"'));
    }

    public async Task<(NotificationPreferencesDto? Preferences, string? Error)> GetNotificationPreferencesAsync()
    {
        var res = await _http.GetAsync("api/users/notification-preferences");
        if (res.IsSuccessStatusCode)
            return (await res.Content.ReadFromJsonAsync<NotificationPreferencesDto>(), null);
        return (null, "Could not load notification preferences.");
    }

    public async Task<(bool Success, string? Error)> UpdateNotificationPreferencesAsync(NotificationPreferencesDto request)
    {
        var res = await _http.PutAsJsonAsync("api/users/notification-preferences", request);
        if (res.IsSuccessStatusCode) return (true, null);
        var body = await res.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? "Could not update preferences." : body.Trim('"'));
    }

    public async Task<(AccountExportDto? Export, string? Error)> ExportDataAsync()
    {
        var res = await _http.GetAsync("api/users/export");
        if (res.IsSuccessStatusCode)
            return (await res.Content.ReadFromJsonAsync<AccountExportDto>(), null);
        return (null, "Could not export your data.");
    }

    /// <summary>
    /// Deletes the current account. On <c>Blocked</c>, <paramref name="Blocked"/> lists the shared
    /// workspaces the user must transfer or delete first (solo-owned ones are removed automatically).
    /// </summary>
    public async Task<(bool Success, List<BlockedWorkspaceDto>? Blocked, string? Error)> DeleteAccountAsync(string password)
    {
        var res = await _http.PostAsJsonAsync("api/users/delete-account", new DeleteAccountRequest { Password = password });
        if (res.IsSuccessStatusCode) return (true, null, null);

        if (res.StatusCode == HttpStatusCode.Conflict)
        {
            var blocked = await res.Content.ReadFromJsonAsync<AccountDeletionBlockedResponse>();
            return (false, blocked?.BlockedWorkspaces ?? [], null);
        }

        var body = await res.Content.ReadAsStringAsync();
        return (false, null, string.IsNullOrWhiteSpace(body) ? "Could not delete your account." : body.Trim('"'));
    }
}
