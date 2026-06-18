namespace Planora.Api.Domain.Entities;

public class ChecklistItem : BaseEntity
{
    public Guid ChecklistId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int Position { get; set; }

    public Checklist Checklist { get; set; } = null!;
}
