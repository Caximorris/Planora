namespace Planora.Shared.DTOs.Column;

public class UpdateColumnRequest
{
    public uint RowVersion { get; set; }
    public string? Title { get; set; }
    public int? Position { get; set; }
    public string? Color { get; set; }
    public bool ClearColor { get; set; }
}
