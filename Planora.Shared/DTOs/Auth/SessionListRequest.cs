namespace Planora.Shared.DTOs.Auth;

public class SessionListRequest
{
    public string CurrentRefreshToken { get; set; } = string.Empty;
}
