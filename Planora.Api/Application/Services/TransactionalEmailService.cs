using Planora.Api.Application.Emails;
using Planora.Api.Application.Interfaces;

namespace Planora.Api.Application.Services;

/// <summary>
/// Composes transactional emails via <see cref="PlanoraEmailFactory"/>, selects the contextual From
/// address via <see cref="IEmailSenderResolver"/>, and sends them through <see cref="IEmailSender"/>.
/// Provider failures are logged and never rethrown. See <see cref="ITransactionalEmailService"/>.
/// </summary>
public sealed class TransactionalEmailService : ITransactionalEmailService
{
    private const string ProductName = "Planora";

    private readonly IEmailSender _sender;
    private readonly IEmailSenderResolver _resolver;
    private readonly IConfiguration _config;
    private readonly ILogger<TransactionalEmailService> _logger;

    public TransactionalEmailService(
        IEmailSender sender,
        IEmailSenderResolver resolver,
        IConfiguration config,
        ILogger<TransactionalEmailService> logger)
    {
        _sender = sender;
        _resolver = resolver;
        _config = config;
        _logger = logger;
    }

    public Task<bool> SendEmailVerificationAsync(string toEmail, string verifyUrl, CancellationToken ct = default) =>
        SendAsync(toEmail, EmailSenderKind.NoReply, b => PlanoraEmailFactory.EmailVerification(b, verifyUrl), ct);

    public Task<bool> SendPasswordResetAsync(string toEmail, string resetUrl, CancellationToken ct = default) =>
        SendAsync(toEmail, EmailSenderKind.NoReply, b => PlanoraEmailFactory.PasswordReset(b, resetUrl), ct);

    public Task<bool> SendPasswordChangedAsync(string toEmail, DateTimeOffset whenUtc, CancellationToken ct = default) =>
        SendAsync(toEmail, EmailSenderKind.Security, b => PlanoraEmailFactory.PasswordChanged(b, whenUtc), ct);

    public Task<bool> SendTwoFactorChangedAsync(string toEmail, bool enabled, DateTimeOffset whenUtc, CancellationToken ct = default) =>
        SendAsync(toEmail, EmailSenderKind.Security, b => PlanoraEmailFactory.TwoFactorChanged(b, enabled, whenUtc), ct);

    public Task<bool> SendWorkspaceInvitationAsync(string toEmail, string workspaceName, string inviterName, string acceptUrl, CancellationToken ct = default) =>
        SendAsync(toEmail, EmailSenderKind.Invites, b => PlanoraEmailFactory.WorkspaceInvitation(b, workspaceName, inviterName, acceptUrl), ct);

    public Task<bool> SendCardAssignedAsync(string toEmail, string cardTitle, string boardUrl, CancellationToken ct = default) =>
        SendAsync(toEmail, EmailSenderKind.Notifications, b => PlanoraEmailFactory.CardAssigned(b, cardTitle, boardUrl), ct);

    public Task<bool> SendCommentAsync(string toEmail, string cardTitle, string commenterName, string boardUrl, CancellationToken ct = default) =>
        SendAsync(toEmail, EmailSenderKind.Notifications, b => PlanoraEmailFactory.NewComment(b, cardTitle, commenterName, boardUrl), ct);

    private async Task<bool> SendAsync(string toEmail, EmailSenderKind kind, Func<EmailBranding, RenderedEmail> compose, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            return false;

        var from = _resolver.Resolve(kind);
        var email = compose(Branding());

        try
        {
            await _sender.SendAsync(new EmailMessage
            {
                To = toEmail,
                Subject = email.Subject,
                HtmlBody = email.HtmlBody,
                TextBody = email.TextBody,
                From = from
            }, ct);
            return true;
        }
        catch (Exception ex)
        {
            // Never let an email failure break the user action that triggered it.
            _logger.LogError(ex, "EMAIL_SEND_FAILED To={To} Subject={Subject}", toEmail, email.Subject);
            return false;
        }
    }

    private EmailBranding Branding() =>
        new(ProductName, _resolver.Resolve(EmailSenderKind.Support).Address, WebBaseUrl());

    // Base URL of the Blazor client for links, from App:WebBaseUrl or the first configured CORS origin.
    private string WebBaseUrl()
    {
        var explicitUrl = _config["App:WebBaseUrl"];
        if (!string.IsNullOrWhiteSpace(explicitUrl))
            return explicitUrl.TrimEnd('/');

        var corsOrigin = _config.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o))
            ?? _config["Cors:AllowedOrigins"]
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(o => !string.IsNullOrWhiteSpace(o));

        return string.IsNullOrWhiteSpace(corsOrigin) ? string.Empty : corsOrigin.TrimEnd('/');
    }
}
