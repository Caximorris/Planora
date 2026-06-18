namespace Planora.Shared.DTOs.Card;

public class CardCommentDto
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
