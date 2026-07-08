namespace Planora.Shared.DTOs.Auth;

public class EnableTwoFactorRequest
{
    /// <summary>Current 6-digit TOTP from the authenticator app, confirming the shared key was stored.</summary>
    public string Code { get; set; } = string.Empty;
}
