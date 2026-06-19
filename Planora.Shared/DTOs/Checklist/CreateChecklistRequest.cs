namespace Planora.Shared.DTOs.Checklist;

public class CreateChecklistRequest
{
    public Guid CardId { get; set; }
    public string Title { get; set; } = string.Empty;
}
