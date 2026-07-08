namespace Planora.Api.Application.Interfaces;

/// <summary>
/// Sends transactional activity emails (assigned to a card, new comment, workspace invite), gated by
/// the recipient's notification preferences. Implementations must never throw into the caller — a
/// mail-provider outage should never fail the underlying user action.
/// </summary>
public interface IActivityEmailNotifier
{
    Task NotifyCardAssignedAsync(string recipientUserId, string cardTitle, Guid boardId, CancellationToken ct = default);

    Task NotifyNewCommentAsync(string recipientUserId, string cardTitle, string commenterName, Guid boardId, CancellationToken ct = default);

    Task NotifyWorkspaceInviteAsync(string inviteeEmail, string workspaceName, string inviterName, string token, CancellationToken ct = default);
}
