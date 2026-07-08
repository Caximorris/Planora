namespace Planora.Api.Domain.Entities;

public class Column : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public int Position { get; set; }
    public string? Color { get; set; }
    public uint RowVersion { get; set; }

    public Guid BoardId { get; set; }
    public Board Board { get; set; } = null!;

    public ICollection<Card> Cards { get; set; } = [];
}
