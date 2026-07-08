namespace Planora.Shared.DTOs.Auth;

/// <summary>
/// Second step of a two-factor login. The email/password are re-verified server-side before the
/// code is checked, and the same progressive lockout as the password step applies.
/// </summary>
public class TwoFactorLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    /// <summary>When true, <see cref="Code"/> is a one-time recovery code rather than a TOTP.</summary>
    public bool IsRecoveryCode { get; set; }
}
