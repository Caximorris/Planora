namespace Planora.Api.Domain.Entities;

public class Board : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverColor { get; set; }
    public string? CoverImageUrl { get; set; }
    public int Position { get; set; }
    public bool IsArchived { get; set; }

    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public ICollection<Column> Columns { get; set; } = [];
}
