namespace Planora.Api.Application.Options;

/// <summary>
/// Binds the <c>Email</c> configuration section that selects the <see cref="Interfaces.IEmailSender"/>
/// backend. <c>Console</c> (default) logs emails for local dev; <c>Resend</c> sends real email via the
/// Resend HTTP API. The API key is a secret — it comes from the gitignored
/// <c>appsettings.Development.json</c> locally and from environment/secret in production, never source.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"Console" (default, dev sink) or "Resend".</summary>
    public string Provider { get; set; } = "Console";

    /// <summary>
    /// The verified sending domain (e.g. <c>planora.website</c>). Used to compose contextual sender
    /// addresses (<c>no-reply@</c>, <c>security@</c>, …). May be overridden by the <c>EMAIL_DOMAIN</c>
    /// environment variable. When blank it is derived from the host of <see cref="From"/>.
    /// </summary>
    public string Domain { get; set; } = "";

    /// <summary>Default/fallback sender identity. <see cref="EmailFromOptions.Address"/> must be on the verified domain.</summary>
    public EmailFromOptions From { get; set; } = new();

    /// <summary>Per-category sender identities resolved by <see cref="Emails.IEmailSenderResolver"/>.</summary>
    public EmailSendersOptions Senders { get; set; } = new();

    public ResendOptions Resend { get; set; } = new();
}

/// <summary>
/// Sender identity. Provide either a full <see cref="Address"/> or a <see cref="LocalPart"/> that is
/// combined with the verified domain (<c>localPart@domain</c>).
/// </summary>
public sealed class EmailFromOptions
{
    public string Address { get; set; } = "";
    public string LocalPart { get; set; } = "";
    public string Name { get; set; } = "Planora";
}

/// <summary>
/// Contextual senders, one per <see cref="Emails.EmailSenderKind"/>. Each is optional in config —
/// unset values fall back to a sensible <c>localPart@domain</c> default. Full addresses can also be
/// supplied via environment variables (<c>EMAIL_FROM_NO_REPLY</c>, <c>EMAIL_FROM_SECURITY</c>, etc.).
/// </summary>
public sealed class EmailSendersOptions
{
    public EmailFromOptions NoReply { get; set; } = new() { LocalPart = "no-reply", Name = "Planora" };
    public EmailFromOptions Security { get; set; } = new() { LocalPart = "security", Name = "Planora Security" };
    public EmailFromOptions Invites { get; set; } = new() { LocalPart = "invites", Name = "Planora Invitations" };
    public EmailFromOptions Notifications { get; set; } = new() { LocalPart = "notifications", Name = "Planora" };
    public EmailFromOptions Support { get; set; } = new() { LocalPart = "support", Name = "Planora Support" };
}

/// <summary>Resend settings, only consumed when <see cref="EmailOptions.Provider"/> is <c>Resend</c>.</summary>
public sealed class ResendOptions
{
    /// <summary>Resend API key (secret; injected via env/secret in prod). Never logged.</summary>
    public string ApiKey { get; set; } = "";
}
