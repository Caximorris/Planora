namespace Planora.Shared.DTOs.Board;

public class BoardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverColor { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? CoverImageFit { get; set; }
    public int Position { get; set; }
    public bool IsArchived { get; set; }
    public uint RowVersion { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid WorkspaceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
