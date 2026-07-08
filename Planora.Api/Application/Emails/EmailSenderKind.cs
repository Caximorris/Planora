namespace Planora.Api.Application.Emails;

/// <summary>
/// The contextual sender identity an email is sent from. Maps to a local-part on the verified
/// sending domain (e.g. <c>no-reply@</c>, <c>security@</c>) via <see cref="IEmailSenderResolver"/>.
/// Centralizing this here keeps sender addresses out of individual flows.
/// </summary>
public enum EmailSenderKind
{
    /// <summary>Automated auth mail the user should not reply to: verification, password reset, OTP.</summary>
    NoReply,

    /// <summary>Security and account alerts: password changed, two-factor enabled/disabled, new sign-in.</summary>
    Security,

    /// <summary>Workspace/board invitations.</summary>
    Invites,

    /// <summary>Product activity notifications: card assignments, comments.</summary>
    Notifications,

    /// <summary>Sender used when a human reply is expected.</summary>
    Support
}
