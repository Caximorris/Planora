namespace Planora.Shared.DTOs.Auth;

/// <summary>
/// One-time recovery codes, returned exactly once (on enable or regenerate). They are hashed at rest
/// and cannot be retrieved again — the client must show them to the user immediately.
/// </summary>
public class TwoFactorRecoveryCodesResponse
{
    public List<string> RecoveryCodes { get; set; } = [];
}
