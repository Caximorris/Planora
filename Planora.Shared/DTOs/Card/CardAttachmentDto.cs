namespace Planora.Shared.DTOs.Card;

public class CardAttachmentDto
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Url { get; set; } = string.Empty;
    public string UploadedById { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
