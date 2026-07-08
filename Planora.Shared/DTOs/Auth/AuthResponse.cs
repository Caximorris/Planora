namespace Planora.Shared.DTOs.Auth;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When true, the password was correct but the account has 2FA enabled: no tokens are issued
    /// and the client must complete login via <c>POST /api/auth/login/2fa</c>. All other fields are
    /// empty in this case.
    /// </summary>
    public bool RequiresTwoFactor { get; set; }
}
