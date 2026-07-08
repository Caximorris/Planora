using System.Net;
using System.Text;

namespace Planora.Api.Application.Emails;

/// <summary>
/// Renders an <see cref="EmailContent"/> into a professional, client-safe HTML document plus a
/// plain-text alternative. The HTML is table-based with inline styles (Outlook/Gmail/Apple Mail
/// compatible), a bulletproof CTA button, a copy/paste fallback link, a preheader, and progressive
/// responsive + dark-mode styles. Every dynamic value is HTML-encoded here — callers pass plain text.
/// </summary>
public static class EmailLayout
{
    // Design tokens (light). Dark variants are applied progressively via the media query below.
    private const string Brand = "#6d28d9";
    private const string PageBg = "#f4f4f7";
    private const string CardBg = "#ffffff";
    private const string Border = "#e5e7eb";
    private const string Text = "#1f2937";
    private const string Muted = "#6b7280";
    private const string FontStack =
        "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif,'Apple Color Emoji','Segoe UI Emoji'";

    public static RenderedEmail Render(string subject, EmailContent content, EmailBranding branding) =>
        new(subject, BuildHtml(content, branding), BuildText(content, branding));

    private static string BuildHtml(EmailContent c, EmailBranding b)
    {
        var product = Enc(b.ProductName);
        var sb = new StringBuilder(4096);

        sb.Append("<!DOCTYPE html>\n");
        sb.Append("<html lang=\"en\" xmlns=\"http://www.w3.org/1999/xhtml\">\n<head>\n");
        sb.Append("<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n");
        sb.Append("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">\n");
        sb.Append("<meta name=\"color-scheme\" content=\"light dark\">\n");
        sb.Append("<meta name=\"supported-color-schemes\" content=\"light dark\">\n");
        sb.Append($"<title>{Enc(c.Preheader)}</title>\n");
        sb.Append("<style>\n");
        sb.Append("  @media only screen and (max-width:600px){.email-card{width:100%!important;border-radius:0!important}.email-pad{padding:24px!important}}\n");
        sb.Append("  @media (prefers-color-scheme:dark){\n");
        sb.Append("    .email-bg{background:#0f1117!important}\n");
        sb.Append("    .email-card{background:#1a1d27!important;border-color:#2a2e3a!important}\n");
        sb.Append("    .email-text{color:#e5e7eb!important}\n");
        sb.Append("    .email-muted{color:#9ca3af!important}\n");
        sb.Append("    .email-link{color:#c4b5fd!important}\n");
        sb.Append("  }\n");
        sb.Append("</style>\n</head>\n");

        sb.Append($"<body class=\"email-bg\" style=\"margin:0;padding:0;background:{PageBg};\">\n");

        // Hidden preheader (inbox preview text), padded so following content doesn't leak into the preview.
        sb.Append($"<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;height:0;width:0;\">{Enc(c.Preheader)}");
        sb.Append(string.Concat(Enumerable.Repeat("&#847;&zwnj;&nbsp;", 30)));
        sb.Append("</div>\n");

        sb.Append($"<table role=\"presentation\" class=\"email-bg\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background:{PageBg};\">\n<tr><td align=\"center\" style=\"padding:32px 12px;\">\n");
        sb.Append($"<table role=\"presentation\" class=\"email-card\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width:600px;max-width:600px;background:{CardBg};border:1px solid {Border};border-radius:12px;overflow:hidden;\">\n");

        // Header wordmark (text, not an external image — better deliverability, no broken image icon).
        sb.Append("<tr><td class=\"email-pad\" style=\"padding:28px 40px 8px;\">\n");
        sb.Append($"<span style=\"font-family:{FontStack};font-size:20px;font-weight:700;letter-spacing:-0.3px;color:{Brand};\">{product}</span>\n");
        sb.Append("</td></tr>\n");

        // Body.
        sb.Append("<tr><td class=\"email-pad\" style=\"padding:8px 40px 32px;\">\n");
        sb.Append($"<h1 class=\"email-text\" style=\"margin:16px 0 16px;font-family:{FontStack};font-size:22px;line-height:1.3;font-weight:700;color:{Text};\">{Enc(c.Heading)}</h1>\n");

        foreach (var para in c.Paragraphs)
            sb.Append($"<p class=\"email-text\" style=\"margin:0 0 16px;font-family:{FontStack};font-size:15px;line-height:1.6;color:{Text};\">{Enc(para)}</p>\n");

        if (!string.IsNullOrWhiteSpace(c.ButtonLabel) && !string.IsNullOrWhiteSpace(c.ButtonUrl))
        {
            var url = Enc(c.ButtonUrl);
            sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:8px 0 24px;\"><tr>\n");
            sb.Append($"<td align=\"center\" bgcolor=\"{Brand}\" style=\"border-radius:8px;background:{Brand};\">\n");
            sb.Append($"<a href=\"{url}\" target=\"_blank\" style=\"display:inline-block;padding:13px 28px;font-family:{FontStack};font-size:15px;font-weight:600;line-height:1;color:#ffffff;text-decoration:none;border-radius:8px;\">{Enc(c.ButtonLabel)}</a>\n");
            sb.Append("</td></tr></table>\n");

            // Copy/paste fallback for clients that strip the button.
            sb.Append($"<p class=\"email-muted\" style=\"margin:0 0 20px;font-family:{FontStack};font-size:13px;line-height:1.6;color:{Muted};\">Or copy and paste this link into your browser:<br>\n");
            sb.Append($"<a class=\"email-link\" href=\"{url}\" target=\"_blank\" style=\"color:{Brand};word-break:break-all;\">{url}</a></p>\n");
        }

        if (!string.IsNullOrWhiteSpace(c.SecondaryNote))
            sb.Append($"<p class=\"email-muted\" style=\"margin:0;font-family:{FontStack};font-size:13px;line-height:1.6;color:{Muted};\">{Enc(c.SecondaryNote)}</p>\n");

        sb.Append("</td></tr>\n");

        // Divider + footer.
        sb.Append($"<tr><td style=\"padding:0 40px;\"><div style=\"border-top:1px solid {Border};\"></div></td></tr>\n");
        sb.Append("<tr><td class=\"email-pad\" style=\"padding:20px 40px 28px;\">\n");
        sb.Append($"<p class=\"email-muted\" style=\"margin:0 0 6px;font-family:{FontStack};font-size:12px;line-height:1.6;color:{Muted};\">{Enc(c.FooterReason)}</p>\n");
        sb.Append($"<p class=\"email-muted\" style=\"margin:0;font-family:{FontStack};font-size:12px;line-height:1.6;color:{Muted};\">{product} &middot; This is an automated message. Need help? <a class=\"email-link\" href=\"mailto:{Enc(b.SupportEmail)}\" style=\"color:{Brand};\">{Enc(b.SupportEmail)}</a></p>\n");
        sb.Append("</td></tr>\n");

        sb.Append("</table>\n</td></tr>\n</table>\n</body>\n</html>");
        return sb.ToString();
    }

    private static string BuildText(EmailContent c, EmailBranding b)
    {
        var sb = new StringBuilder(1024);
        sb.Append(b.ProductName).Append("\n\n");
        sb.Append(c.Heading).Append("\n\n");

        foreach (var para in c.Paragraphs)
            sb.Append(para).Append("\n\n");

        if (!string.IsNullOrWhiteSpace(c.ButtonLabel) && !string.IsNullOrWhiteSpace(c.ButtonUrl))
            sb.Append(c.ButtonLabel).Append(":\n").Append(c.ButtonUrl).Append("\n\n");

        if (!string.IsNullOrWhiteSpace(c.SecondaryNote))
            sb.Append(c.SecondaryNote).Append("\n\n");

        sb.Append("—\n");
        sb.Append(c.FooterReason).Append('\n');
        sb.Append(b.ProductName).Append(" · This is an automated message. Need help? ").Append(b.SupportEmail).Append('\n');
        return sb.ToString();
    }

    private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
