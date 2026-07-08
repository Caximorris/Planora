namespace Planora.Shared.DTOs.Auth;

/// <summary>
/// Disabling 2FA (or regenerating recovery codes) requires re-verification so a hijacked session
/// cannot silently weaken the account. <see cref="Code"/> is a TOTP or, when
/// <see cref="IsRecoveryCode"/> is set, a one-time recovery code.
/// </summary>
public class DisableTwoFactorRequest
{
    public string Code { get; set; } = string.Empty;
    public bool IsRecoveryCode { get; set; }
}
