namespace Planora.Shared.DTOs.Checklist;

public class ChecklistItemDto
{
    public Guid Id { get; set; }
    public Guid ChecklistId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int Position { get; set; }
}
