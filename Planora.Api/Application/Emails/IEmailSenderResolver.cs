namespace Planora.Api.Application.Emails;

/// <summary>Resolves the contextual From address/display name for a given <see cref="EmailSenderKind"/>.</summary>
public interface IEmailSenderResolver
{
    EmailAddress Resolve(EmailSenderKind kind);

    /// <summary>The sending domain in use (verified with the provider), for building branding/support links.</summary>
    string Domain { get; }
}
