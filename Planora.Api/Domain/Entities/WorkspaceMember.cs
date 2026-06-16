using Planora.Shared.Enums;

namespace Planora.Api.Domain.Entities;

public class WorkspaceMember
{
    public Guid WorkspaceId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public WorkspaceRole Role { get; set; } = WorkspaceRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Workspace Workspace { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
