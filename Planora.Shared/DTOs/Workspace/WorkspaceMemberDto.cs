using Planora.Shared.Enums;

namespace Planora.Shared.DTOs.Workspace;

public class WorkspaceMemberDto
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public WorkspaceRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
}
