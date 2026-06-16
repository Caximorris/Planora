namespace Planora.Shared.DTOs.Board;

public class UpdateBoardRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool ClearDescription { get; set; }
    public string? CoverColor { get; set; }
    public int? Position { get; set; }
}
