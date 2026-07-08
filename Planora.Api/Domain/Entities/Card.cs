using Planora.Shared.Enums;

namespace Planora.Api.Domain.Entities;

public class Card : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Position { get; set; }
    public DateTime? DueDate { get; set; }
    public CardPriority Priority { get; set; } = CardPriority.None;
    public string? Color { get; set; }
    public bool IsArchived { get; set; }

    // Soft-delete (trash): null = live, non-null = trashed. Distinct from IsArchived (put-aside).
    public DateTime? DeletedAt { get; set; }

    public Guid ColumnId { get; set; }
    public Column Column { get; set; } = null!;

    public string? AssigneeId { get; set; }
    public AppUser? Assignee { get; set; }

    public ICollection<CardComment> Comments { get; set; } = [];
    public ICollection<CardLabel> Labels { get; set; } = [];
    public ICollection<Checklist> Checklists { get; set; } = [];
}
