namespace Planora.Api.Domain.Entities;

public class CardComment : BaseEntity
{
    public Guid CardId { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;

    public Card Card { get; set; } = null!;
    public AppUser Author { get; set; } = null!;
}
