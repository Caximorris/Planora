namespace Planora.Api.Domain.Entities;

public class CardAttachment : BaseEntity
{
    public Guid CardId { get; set; }
    public string UploadedById { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Url { get; set; } = string.Empty;

    public Card Card { get; set; } = null!;
    public AppUser UploadedBy { get; set; } = null!;
}
