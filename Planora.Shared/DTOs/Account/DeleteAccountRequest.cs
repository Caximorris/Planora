namespace Planora.Shared.DTOs.Account;

/// <summary>
/// Body for permanent account deletion. The current password is required as a re-authentication
/// step for this destructive, irreversible action.
/// </summary>
public class DeleteAccountRequest
{
    public string Password { get; set; } = string.Empty;
}
