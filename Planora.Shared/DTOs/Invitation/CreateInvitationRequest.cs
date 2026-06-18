using Planora.Shared.Enums;

namespace Planora.Shared.DTOs.Invitation;

public class CreateInvitationRequest
{
    public string InviteeEmail { get; set; } = string.Empty;
    public WorkspaceRole Role { get; set; } = WorkspaceRole.Member;
}
