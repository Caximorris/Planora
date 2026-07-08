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

    public EmailFromOptions From { get; set; } = new();

    public ResendOptions Resend { get; set; } = new();
}

/// <summary>Sender identity. <see cref="Address"/> must be on a domain verified with the provider.</summary>
public sealed class EmailFromOptions
{
    public string Address { get; set; } = "";
    public string Name { get; set; } = "Planora";
}

/// <summary>Resend settings, only consumed when <see cref="EmailOptions.Provider"/> is <c>Resend</c>.</summary>
public sealed class ResendOptions
{
    /// <summary>Resend API key (secret; injected via env/secret in prod). Never logged.</summary>
    public string ApiKey { get; set; } = "";
}
