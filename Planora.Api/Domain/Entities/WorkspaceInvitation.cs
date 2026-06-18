using Planora.Shared.Enums;

namespace Planora.Api.Domain.Entities;

public class WorkspaceInvitation : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public string InviterUserId { get; set; } = string.Empty;
    public string InviteeEmail { get; set; } = string.Empty;
    public WorkspaceRole Role { get; set; } = WorkspaceRole.Member;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public Workspace Workspace { get; set; } = null!;
    public AppUser Inviter { get; set; } = null!;
}
