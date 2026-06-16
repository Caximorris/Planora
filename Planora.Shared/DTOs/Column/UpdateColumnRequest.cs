namespace Planora.Shared.DTOs.Column;

public class UpdateColumnRequest
{
    public string? Title { get; set; }
    public int? Position { get; set; }
    public string? Color { get; set; }
    public bool ClearColor { get; set; }
}
