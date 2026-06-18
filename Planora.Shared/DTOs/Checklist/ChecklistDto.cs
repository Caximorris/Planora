namespace Planora.Shared.DTOs.Checklist;

public class ChecklistDto
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Position { get; set; }
    public List<ChecklistItemDto> Items { get; set; } = [];
}
