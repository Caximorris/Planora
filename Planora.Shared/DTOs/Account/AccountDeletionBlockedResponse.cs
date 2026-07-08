namespace Planora.Shared.DTOs.Account;

/// <summary>
/// Returned with HTTP 409 when account deletion is blocked because the user still owns one or more
/// workspaces that have other members. Those workspaces must be transferred to another owner or
/// deleted before the account can be removed. Solo-owned workspaces are removed automatically and
/// never appear here.
/// </summary>
public class AccountDeletionBlockedResponse
{
    public List<BlockedWorkspaceDto> BlockedWorkspaces { get; set; } = [];
}

/// <summary>A workspace that prevents account deletion because the user owns it and it has other members.</summary>
public class BlockedWorkspaceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Total members including the owner (always &gt; 1 for a blocking workspace).</summary>
    public int MemberCount { get; set; }
}
