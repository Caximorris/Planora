using Planora.Shared.Enums;

namespace Planora.Shared.DTOs.Invitation;

public class InvitationDto
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string WorkspaceName { get; set; } = string.Empty;
    public string InviterName { get; set; } = string.Empty;
    public string InviteeEmail { get; set; } = string.Empty;
    public WorkspaceRole Role { get; set; }
    public InvitationStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Token { get; set; } = string.Empty;
}
