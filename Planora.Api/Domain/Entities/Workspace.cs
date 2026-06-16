namespace Planora.Api.Domain.Entities;

public class Workspace : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string OwnerId { get; set; } = string.Empty;
    public AppUser Owner { get; set; } = null!;

    public ICollection<Board> Boards { get; set; } = [];
    public ICollection<WorkspaceMember> Members { get; set; } = [];
}
