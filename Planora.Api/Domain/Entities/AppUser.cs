using Microsoft.AspNetCore.Identity;

namespace Planora.Api.Domain.Entities;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Email-notification preferences (default on). Gate the activity emails sent by ActivityEmailNotifier.
    public bool EmailOnAssigned { get; set; } = true;
    public bool EmailOnComment { get; set; } = true;
    public bool EmailOnWorkspaceInvite { get; set; } = true;

    public ICollection<Workspace> OwnedWorkspaces { get; set; } = [];
    public ICollection<WorkspaceMember> WorkspaceMemberships { get; set; } = [];
}
