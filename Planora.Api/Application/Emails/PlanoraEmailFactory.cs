using System.Globalization;

namespace Planora.Api.Application.Emails;

/// <summary>
/// Builds each transactional email type as a <see cref="RenderedEmail"/> using the shared
/// <see cref="EmailLayout"/>. Pure and deterministic (no I/O), so every template is unit-testable.
/// Wording is deliberately short, action-focused, and free of marketing/spam phrasing. Dynamic
/// values are passed as plain text — the layout handles all HTML escaping.
/// </summary>
public static class PlanoraEmailFactory
{
    // ---- Auth (no-reply) ----------------------------------------------------------------------

    public static RenderedEmail EmailVerification(EmailBranding b, string verifyUrl) =>
        EmailLayout.Render($"Verify your {b.ProductName} email", new EmailContent
        {
            Preheader = $"Confirm your email address to finish setting up your {b.ProductName} account.",
            Heading = "Verify your email address",
            Paragraphs =
            [
                $"Welcome to {b.ProductName}. Confirm this email address to activate your account and start collaborating.",
            ],
            ButtonLabel = "Verify email address",
            ButtonUrl = verifyUrl,
            SecondaryNote = $"This link expires in 1 hour. If you didn't create a {b.ProductName} account, you can safely ignore this email.",
            FooterReason = $"You received this email because this address was used to create a {b.ProductName} account.",
        }, b);

    public static RenderedEmail PasswordReset(EmailBranding b, string resetUrl) =>
        EmailLayout.Render($"Reset your {b.ProductName} password", new EmailContent
        {
            Preheader = $"Reset the password for your {b.ProductName} account.",
            Heading = "Reset your password",
            Paragraphs =
            [
                $"We received a request to reset the password for your {b.ProductName} account. Choose a new password using the button below.",
            ],
            ButtonLabel = "Reset password",
            ButtonUrl = resetUrl,
            SecondaryNote = "This link expires in 1 hour. If you didn't request a password reset, you can ignore this email — your password will not change.",
            FooterReason = $"You received this email because a password reset was requested for your {b.ProductName} account.",
        }, b);

    // ---- Security alerts (security@) ----------------------------------------------------------

    public static RenderedEmail PasswordChanged(EmailBranding b, DateTimeOffset whenUtc) =>
        SecurityAlert(b,
            subject: $"Your {b.ProductName} password was changed",
            heading: "Your password was changed",
            summary: $"The password for your {b.ProductName} account was just changed.",
            whenUtc: whenUtc);

    public static RenderedEmail TwoFactorChanged(EmailBranding b, bool enabled, DateTimeOffset whenUtc) =>
        SecurityAlert(b,
            subject: enabled
                ? "Two-factor authentication was enabled"
                : "Two-factor authentication was disabled",
            heading: enabled
                ? "Two-factor authentication enabled"
                : "Two-factor authentication disabled",
            summary: enabled
                ? $"Two-factor authentication was turned on for your {b.ProductName} account. Your account is now protected by an authenticator app."
                : $"Two-factor authentication was turned off for your {b.ProductName} account.",
            whenUtc: whenUtc);

    private static RenderedEmail SecurityAlert(EmailBranding b, string subject, string heading, string summary, DateTimeOffset whenUtc) =>
        EmailLayout.Render(subject, new EmailContent
        {
            Preheader = summary,
            Heading = heading,
            Paragraphs =
            [
                summary,
                $"When: {whenUtc.UtcDateTime.ToString("dddd, d MMMM yyyy 'at' HH:mm 'UTC'", CultureInfo.InvariantCulture)}",
            ],
            ButtonLabel = "Review account security",
            ButtonUrl = $"{b.WebUrl}/profile",
            SecondaryNote = "If this was you, no action is needed. If you don't recognize this activity, reset your password immediately and review your account security.",
            FooterReason = $"You received this security alert because of activity on your {b.ProductName} account.",
        }, b);

    // ---- Invitations (invites@) ---------------------------------------------------------------

    public static RenderedEmail WorkspaceInvitation(EmailBranding b, string workspaceName, string inviterName, string acceptUrl) =>
        EmailLayout.Render($"You've been invited to {workspaceName} on {b.ProductName}", new EmailContent
        {
            Preheader = $"{inviterName} invited you to the {workspaceName} workspace on {b.ProductName}.",
            Heading = "You've been invited to a workspace",
            Paragraphs =
            [
                $"{inviterName} invited you to collaborate on the \"{workspaceName}\" workspace in {b.ProductName}.",
                "Accept the invitation to join the workspace and start working together.",
            ],
            ButtonLabel = "Accept invitation",
            ButtonUrl = acceptUrl,
            SecondaryNote = "If you weren't expecting this invitation, you can safely ignore this email.",
            FooterReason = $"You received this email because {inviterName} invited you to a workspace on {b.ProductName}.",
        }, b);

    // ---- Activity notifications (notifications@) ----------------------------------------------

    public static RenderedEmail CardAssigned(EmailBranding b, string cardTitle, string boardUrl) =>
        EmailLayout.Render("You were assigned to a card", new EmailContent
        {
            Preheader = $"You were assigned to \"{cardTitle}\".",
            Heading = "You were assigned a card",
            Paragraphs =
            [
                $"You've been assigned to \"{cardTitle}\" on {b.ProductName}.",
            ],
            ButtonLabel = "Open board",
            ButtonUrl = boardUrl,
            FooterReason = $"You received this email because you have card-assignment notifications enabled. Manage them in {b.ProductName} under Profile → Notifications.",
        }, b);

    public static RenderedEmail NewComment(EmailBranding b, string cardTitle, string commenterName, string boardUrl) =>
        EmailLayout.Render("New comment on your card", new EmailContent
        {
            Preheader = $"{commenterName} commented on \"{cardTitle}\".",
            Heading = "New comment on your card",
            Paragraphs =
            [
                $"{commenterName} commented on \"{cardTitle}\".",
            ],
            ButtonLabel = "View comment",
            ButtonUrl = boardUrl,
            FooterReason = $"You received this email because you have comment notifications enabled. Manage them in {b.ProductName} under Profile → Notifications.",
        }, b);
}
