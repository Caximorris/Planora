namespace Planora.Shared.DTOs.Auth;

public class TwoFactorStatusResponse
{
    public bool Enabled { get; set; }
    public int RecoveryCodesRemaining { get; set; }
}
