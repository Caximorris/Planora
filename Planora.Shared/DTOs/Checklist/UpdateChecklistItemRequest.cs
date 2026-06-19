namespace Planora.Shared.DTOs.Checklist;

public class UpdateChecklistItemRequest
{
    public string? Text { get; set; }
    public bool? IsCompleted { get; set; }
    public int? Position { get; set; }
}
