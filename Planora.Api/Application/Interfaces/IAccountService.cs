using Planora.Shared.DTOs.Account;

namespace Planora.Api.Application.Interfaces;

/// <summary>
/// Account-wide operations that span the user and all of their workspace data: exporting a portable
/// copy and permanently deleting the account. Kept out of the controller because both walk the full
/// workspace graph and enforce ownership rules.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Builds a full export of the user's profile and every workspace they belong to. Returns
    /// <c>null</c> if the user no longer exists.
    /// </summary>
    Task<AccountExportDto?> BuildExportAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Permanently deletes the account. Solo-owned workspaces are removed with it; if the user owns
    /// any workspace with other members, deletion is blocked and those workspaces are returned so the
    /// caller can prompt for transfer/deletion first.
    /// </summary>
    Task<AccountDeletionResult> DeleteAccountAsync(string userId, string password, CancellationToken ct = default);
}

public enum AccountDeletionStatus
{
    Success,
    WrongPassword,
    Blocked,
    NotFound,
    Error
}

/// <summary>Outcome of <see cref="IAccountService.DeleteAccountAsync"/>.</summary>
public sealed record AccountDeletionResult(
    AccountDeletionStatus Status,
    IReadOnlyList<BlockedWorkspaceDto>? BlockedWorkspaces = null,
    string? Error = null)
{
    public static readonly AccountDeletionResult Success = new(AccountDeletionStatus.Success);
    public static readonly AccountDeletionResult WrongPassword = new(AccountDeletionStatus.WrongPassword);
    public static readonly AccountDeletionResult NotFound = new(AccountDeletionStatus.NotFound);

    public static AccountDeletionResult Blocked(IReadOnlyList<BlockedWorkspaceDto> workspaces) =>
        new(AccountDeletionStatus.Blocked, BlockedWorkspaces: workspaces);

    public static AccountDeletionResult Failed(string error) =>
        new(AccountDeletionStatus.Error, Error: error);
}
