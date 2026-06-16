namespace Planora.Shared.DTOs.Board;

public class CreateBoardRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverColor { get; set; }
    public Guid WorkspaceId { get; set; }
}
