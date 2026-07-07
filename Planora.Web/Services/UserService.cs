using System.Net.Http.Json;
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
}
