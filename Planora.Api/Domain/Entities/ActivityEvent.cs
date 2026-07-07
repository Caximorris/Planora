namespace Planora.Api.Domain.Entities;

public class ActivityEvent : BaseEntity
{
    public string ActorUserId { get; set; } = string.Empty;
    public AppUser Actor { get; set; } = null!;

    public string Verb { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }

    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public Guid? BoardId { get; set; }
    public Board? Board { get; set; }

    public string PayloadJson { get; set; } = "{}";
}
