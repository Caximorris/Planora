using Planora.Shared.Enums;

namespace Planora.Shared.DTOs.Card;

public class UpdateCardRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public Guid? ColumnId { get; set; }
    public int? Position { get; set; }
    public CardPriority? Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public string? AssigneeId { get; set; }
    public string? Color { get; set; }
    public bool ClearColor { get; set; }
    public bool ClearDescription { get; set; }
    public bool ClearAssignee { get; set; }
}
