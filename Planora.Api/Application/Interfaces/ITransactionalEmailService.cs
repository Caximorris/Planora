namespace Planora.Api.Application.Interfaces;

/// <summary>
/// High-level sender for transactional emails. Each method composes the email from the shared
/// template system, resolves the correct contextual From address, and delivers it via
/// <see cref="IEmailSender"/>. All methods swallow and log provider failures (returning
/// <c>false</c>) so a mail outage never breaks the user action that triggered the send.
/// Recipient preference gating is the caller's responsibility (see <see cref="IActivityEmailNotifier"/>).
/// </summary>
public interface ITransactionalEmailService
{
    Task<bool> SendEmailVerificationAsync(string toEmail, string verifyUrl, CancellationToken ct = default);

    Task<bool> SendPasswordResetAsync(string toEmail, string resetUrl, CancellationToken ct = default);

    Task<bool> SendPasswordChangedAsync(string toEmail, DateTimeOffset whenUtc, CancellationToken ct = default);

    Task<bool> SendTwoFactorChangedAsync(string toEmail, bool enabled, DateTimeOffset whenUtc, CancellationToken ct = default);

    Task<bool> SendWorkspaceInvitationAsync(string toEmail, string workspaceName, string inviterName, string acceptUrl, CancellationToken ct = default);

    Task<bool> SendCardAssignedAsync(string toEmail, string cardTitle, string boardUrl, CancellationToken ct = default);

    Task<bool> SendCommentAsync(string toEmail, string cardTitle, string commenterName, string boardUrl, CancellationToken ct = default);
}
