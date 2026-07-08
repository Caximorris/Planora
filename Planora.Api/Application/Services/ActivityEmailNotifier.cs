using System.Net;
using Microsoft.AspNetCore.Identity;
using Planora.Api.Application.Interfaces;
using Planora.Api.Domain.Entities;

namespace Planora.Api.Application.Services;

/// <summary>
/// Builds and sends activity emails via <see cref="IEmailSender"/>, respecting each recipient's
/// notification preferences. All sends are wrapped so a provider failure is logged but never bubbles
/// up into the request that triggered it.
/// </summary>
public sealed class ActivityEmailNotifier : IActivityEmailNotifier
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;
    private readonly ILogger<ActivityEmailNotifier> _logger;

    public ActivityEmailNotifier(
        UserManager<AppUser> userManager,
        IEmailSender emailSender,
        IConfiguration config,
        ILogger<ActivityEmailNotifier> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _config = config;
        _logger = logger;
    }

    public async Task NotifyCardAssignedAsync(string recipientUserId, string cardTitle, Guid boardId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(recipientUserId);
        if (user?.Email is null || !user.EmailOnAssigned) return;

        await SendAsync(user.Email, "You were assigned to a card",
            "You've been assigned to a card",
            $"You were assigned to <strong>{Encode(cardTitle)}</strong> on Planora.",
            "Open the board", $"{WebBaseUrl()}/boards/{boardId}", ct);
    }

    public async Task NotifyNewCommentAsync(string recipientUserId, string cardTitle, string commenterName, Guid boardId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(recipientUserId);
        if (user?.Email is null || !user.EmailOnComment) return;

        await SendAsync(user.Email, "New comment on your card",
            "New comment",
            $"{Encode(commenterName)} commented on <strong>{Encode(cardTitle)}</strong>.",
            "View the comment", $"{WebBaseUrl()}/boards/{boardId}", ct);
    }

    public async Task NotifyWorkspaceInviteAsync(string inviteeEmail, string workspaceName, string inviterName, string token, CancellationToken ct = default)
    {
        // The invite is transactional: send unless the invitee is a known user who opted out.
        var user = await _userManager.FindByEmailAsync(inviteeEmail);
        if (user is not null && !user.EmailOnWorkspaceInvite) return;

        await SendAsync(inviteeEmail, $"You've been invited to {workspaceName} on Planora",
            "Workspace invitation",
            $"{Encode(inviterName)} invited you to join <strong>{Encode(workspaceName)}</strong> on Planora.",
            "View invitation", $"{WebBaseUrl()}/invite/{Uri.EscapeDataString(token)}", ct);
    }

    private async Task SendAsync(string to, string subject, string heading, string body, string ctaText, string ctaUrl, CancellationToken ct)
    {
        try
        {
            await _emailSender.SendAsync(to, subject, BuildHtml(heading, body, ctaText, ctaUrl), ct);
        }
        catch (Exception ex)
        {
            // Never let an email failure break the user action that triggered it.
            _logger.LogError(ex, "ACTIVITY_EMAIL_FAILED To={To} Subject={Subject}", to, subject);
        }
    }

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

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string BuildHtml(string heading, string body, string ctaText, string ctaUrl) =>
        $"""
        <div style="font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;max-width:480px;margin:0 auto;color:#1a1a2e;">
          <h2 style="color:#6d28d9;margin:0 0 12px;">{heading}</h2>
          <p style="font-size:15px;line-height:1.5;margin:0 0 20px;">{body}</p>
          <p style="margin:0 0 24px;">
            <a href="{ctaUrl}" style="display:inline-block;background:#6d28d9;color:#fff;text-decoration:none;padding:10px 20px;border-radius:8px;font-weight:600;">{ctaText}</a>
          </p>
          <p style="font-size:12px;color:#8a8a9e;margin:0;">You're receiving this because of your Planora notification settings. You can change them in Profile &rarr; Notifications.</p>
        </div>
        """;
}
