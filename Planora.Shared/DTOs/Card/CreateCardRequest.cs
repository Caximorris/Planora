using Planora.Shared.Enums;

namespace Planora.Shared.DTOs.Card;

public class CreateCardRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ColumnId { get; set; }
    public CardPriority Priority { get; set; } = CardPriority.None;
    public DateTime? DueDate { get; set; }
}
