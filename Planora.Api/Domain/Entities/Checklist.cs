namespace Planora.Api.Domain.Entities;

public class Checklist : BaseEntity
{
    public Guid CardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Position { get; set; }

    public Card Card { get; set; } = null!;
    public ICollection<ChecklistItem> Items { get; set; } = [];
}
