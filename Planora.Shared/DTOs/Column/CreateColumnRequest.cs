namespace Planora.Shared.DTOs.Column;

public class CreateColumnRequest
{
    public string Title { get; set; } = string.Empty;
    public Guid BoardId { get; set; }
}
