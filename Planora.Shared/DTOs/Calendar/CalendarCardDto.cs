using Planora.Shared.Enums;

namespace Planora.Shared.DTOs.Calendar;

public class CalendarCardDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public Guid BoardId { get; set; }
    public string BoardName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public CardPriority Priority { get; set; }
    public bool IsOverdue { get; set; }
    public string? AssigneeDisplayName { get; set; }
}
