namespace Planora.Shared.DTOs.Auth;

public class RevokeOtherSessionsRequest
{
    public string CurrentRefreshToken { get; set; } = string.Empty;
}
