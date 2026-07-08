using Microsoft.Extensions.Options;
using Planora.Api.Application.Options;

namespace Planora.Api.Application.Emails;

/// <summary>
/// Central mapping of <see cref="EmailSenderKind"/> to a From address on the verified sending domain.
///
/// Resolution order for each sender's address, highest priority first:
///   1. Environment override (<c>EMAIL_FROM_NO_REPLY</c>, <c>EMAIL_FROM_SECURITY</c>, …) — a full address.
///   2. An explicit <c>Email:Senders:*:Address</c> from configuration.
///   3. <c>localPart@domain</c>, where the local part is <c>Email:Senders:*:LocalPart</c> and the domain
///      comes from <c>EMAIL_DOMAIN</c> / <c>Email:Domain</c> / the host of <c>Email:From:Address</c>.
///
/// Since Resend verifies at the domain level, every local part on the verified domain is deliverable
/// with the single configured API key — no per-address setup is required.
/// </summary>
public sealed class EmailSenderResolver : IEmailSenderResolver
{
    private readonly EmailOptions _options;
    private readonly IConfiguration _config;

    public EmailSenderResolver(IOptions<EmailOptions> options, IConfiguration config)
    {
        _options = options.Value;
        _config = config;
    }

    public string Domain
    {
        get
        {
            var envDomain = _config["EMAIL_DOMAIN"];
            if (!string.IsNullOrWhiteSpace(envDomain))
                return envDomain.Trim();
            if (!string.IsNullOrWhiteSpace(_options.Domain))
                return _options.Domain.Trim();

            // Fall back to the domain of the default From address (the historically verified sender).
            var at = _options.From.Address.IndexOf('@');
            return at >= 0 && at < _options.From.Address.Length - 1
                ? _options.From.Address[(at + 1)..].Trim()
                : string.Empty;
        }
    }

    public EmailAddress Resolve(EmailSenderKind kind)
    {
        var (envVar, sender, defaultLocalPart, defaultName) = kind switch
        {
            EmailSenderKind.NoReply => ("EMAIL_FROM_NO_REPLY", _options.Senders.NoReply, "no-reply", _options.From.Name),
            EmailSenderKind.Security => ("EMAIL_FROM_SECURITY", _options.Senders.Security, "security", $"{_options.From.Name} Security"),
            EmailSenderKind.Invites => ("EMAIL_FROM_INVITES", _options.Senders.Invites, "invites", $"{_options.From.Name} Invitations"),
            EmailSenderKind.Notifications => ("EMAIL_FROM_NOTIFICATIONS", _options.Senders.Notifications, "notifications", _options.From.Name),
            EmailSenderKind.Support => ("EMAIL_FROM_SUPPORT", _options.Senders.Support, "support", $"{_options.From.Name} Support"),
            _ => ("EMAIL_FROM_NO_REPLY", _options.Senders.NoReply, "no-reply", _options.From.Name),
        };

        var address = FirstNonEmpty(
            _config[envVar],
            sender.Address,
            ComposeAddress(sender.LocalPart, defaultLocalPart));

        var name = FirstNonEmpty(sender.Name, defaultName, _options.From.Name);
        return new EmailAddress(address, name);
    }

    private string ComposeAddress(string? configuredLocalPart, string fallbackLocalPart)
    {
        var localPart = FirstNonEmpty(configuredLocalPart, fallbackLocalPart);
        var domain = Domain;

        // If no domain is resolvable, fall back to the default configured sender so we never emit
        // an invalid bare local part.
        return string.IsNullOrWhiteSpace(domain) ? _options.From.Address : $"{localPart}@{domain}";
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
}
