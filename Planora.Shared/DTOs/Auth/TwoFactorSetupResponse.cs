namespace Planora.Shared.DTOs.Auth;

/// <summary>
/// Enrollment data returned when a user begins 2FA setup. <see cref="SharedKey"/> is the manual-entry
/// secret (grouped for readability); <see cref="AuthenticatorUri"/> is the otpauth:// URI a QR code
/// encodes. 2FA is not yet enabled at this point — the user must confirm a code via /2fa/enable.
/// </summary>
public class TwoFactorSetupResponse
{
    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
}
