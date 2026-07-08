namespace Planora.Api.Application.Emails;

/// <summary>A named email address. <see cref="Format"/> produces an RFC 5322 <c>Name &lt;addr&gt;</c> string.</summary>
public sealed record EmailAddress(string Address, string? Name = null)
{
    public string Format() =>
        string.IsNullOrWhiteSpace(Name) ? Address : $"{Name} <{Address}>";
}

/// <summary>
/// A fully-composed transactional email handed to <see cref="Interfaces.IEmailSender"/>.
/// <see cref="From"/> is optional — when null the provider falls back to its default configured sender.
/// <see cref="TextBody"/> is the plain-text alternative for clients that prefer it and for deliverability.
/// </summary>
public sealed record EmailMessage
{
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }
    public string? TextBody { get; init; }
    public EmailAddress? From { get; init; }
}
