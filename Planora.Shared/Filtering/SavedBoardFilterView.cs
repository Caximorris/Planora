namespace Planora.Shared.Filtering;

public class SavedBoardFilterView
{
    public string Name { get; set; } = string.Empty;
    public BoardFilterState Filters { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
