using Microsoft.AspNetCore.Identity;

namespace Planora.Api.Domain.Entities;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Workspace> OwnedWorkspaces { get; set; } = [];
    public ICollection<WorkspaceMember> WorkspaceMemberships { get; set; } = [];
}
