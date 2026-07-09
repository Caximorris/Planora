namespace Planora.Api.Domain.Entities;

public class Board : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverColor { get; set; }
    public string? CoverImageUrl { get; set; }
    // How the cover image fills the board background: "cover" (fill + crop, default) or "contain"
    // (whole image visible). Null is treated as "cover".
    public string? CoverImageFit { get; set; }
    public int Position { get; set; }
    public bool IsArchived { get; set; }
    public uint RowVersion { get; set; }

    // Soft-delete (trash): null = live, non-null = trashed. Distinct from IsArchived (put-aside).
    public DateTime? DeletedAt { get; set; }

    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public ICollection<Column> Columns { get; set; } = [];
}
