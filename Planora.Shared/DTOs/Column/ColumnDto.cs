using Planora.Shared.DTOs.Card;

namespace Planora.Shared.DTOs.Column;

public class ColumnDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Position { get; set; }
    public string? Color { get; set; }
    public uint RowVersion { get; set; }
    public Guid BoardId { get; set; }
    public List<CardDto> Cards { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
