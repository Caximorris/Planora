using Microsoft.AspNetCore.Identity;
using Planora.Api.Application.Interfaces;
using Planora.Api.Domain.Entities;

namespace Planora.Api.Application.Services;

/// <summary>
/// Gates activity emails by each recipient's notification preferences, then delegates composition and
/// delivery to <see cref="ITransactionalEmailService"/> (which handles templating, sender selection,
/// and swallowing provider failures). Board/invite links are built from configured base URLs.
/// </summary>
public sealed class ActivityEmailNotifier : IActivityEmailNotifier
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITransactionalEmailService _email;
    private readonly IConfiguration _config;

    public ActivityEmailNotifier(
        UserManager<AppUser> userManager,
        ITransactionalEmailService email,
        IConfiguration config)
    {
        _userManager = userManager;
        _email = email;
        _config = config;
    }

    public async Task NotifyCardAssignedAsync(string recipientUserId, string cardTitle, Guid boardId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(recipientUserId);
        if (user?.Email is null || !user.EmailOnAssigned) return;

        await _email.SendCardAssignedAsync(user.Email, cardTitle, $"{WebBaseUrl()}/boards/{boardId}", ct);
    }

    public async Task NotifyNewCommentAsync(string recipientUserId, string cardTitle, string commenterName, Guid boardId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(recipientUserId);
        if (user?.Email is null || !user.EmailOnComment) return;

        await _email.SendCommentAsync(user.Email, cardTitle, commenterName, $"{WebBaseUrl()}/boards/{boardId}", ct);
    }

    public async Task NotifyWorkspaceInviteAsync(string inviteeEmail, string workspaceName, string inviterName, string token, CancellationToken ct = default)
    {
        // The invite is transactional: send unless the invitee is a known user who opted out.
        var user = await _userManager.FindByEmailAsync(inviteeEmail);
        if (user is not null && !user.EmailOnWorkspaceInvite) return;

        var acceptUrl = $"{WebBaseUrl()}/invite/{Uri.EscapeDataString(token)}";
        await _email.SendWorkspaceInvitationAsync(inviteeEmail, workspaceName, inviterName, acceptUrl, ct);
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
}
