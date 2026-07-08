namespace Planora.Api.Application.Emails;

/// <summary>
/// Product identity injected into the shared layout (header wordmark, footer support link, web URL).
/// Values come from configuration/the sender resolver so nothing (domain, support address) is hardcoded.
/// </summary>
public sealed record EmailBranding(string ProductName, string SupportEmail, string WebUrl);

/// <summary>
/// The semantic content of a single email, independent of markup. All string fields are treated as
/// plain text and are HTML-encoded by <see cref="EmailLayout"/> when rendering — callers must NOT
/// pre-encode. This keeps escaping in one place and makes the plain-text alternative trivial to build.
/// </summary>
public sealed record EmailContent
{
    /// <summary>Document title and hidden preview text shown by inbox clients.</summary>
    public required string Preheader { get; init; }

    /// <summary>Main headline.</summary>
    public required string Heading { get; init; }

    /// <summary>Body copy, one entry per paragraph.</summary>
    public required IReadOnlyList<string> Paragraphs { get; init; }

    /// <summary>Primary call-to-action label. When null (with <see cref="ButtonUrl"/>), no button renders.</summary>
    public string? ButtonLabel { get; init; }

    /// <summary>Primary call-to-action target. Also shown as a copy/paste fallback link.</summary>
    public string? ButtonUrl { get; init; }

    /// <summary>Muted note under the CTA, e.g. expiry and "if you didn't request this" guidance.</summary>
    public string? SecondaryNote { get; init; }

    /// <summary>Footer line explaining why the recipient received this email.</summary>
    public required string FooterReason { get; init; }
}

/// <summary>A composed email ready to send: subject line plus HTML and plain-text bodies.</summary>
public sealed record RenderedEmail(string Subject, string HtmlBody, string TextBody);
