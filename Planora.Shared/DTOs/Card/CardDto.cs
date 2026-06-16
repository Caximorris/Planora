using Planora.Shared.Enums;

namespace Planora.Shared.DTOs.Card;

public class CardDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Position { get; set; }
    public DateTime? DueDate { get; set; }
    public CardPriority Priority { get; set; }
    public string? Color { get; set; }
    public Guid ColumnId { get; set; }
    public string? AssigneeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
