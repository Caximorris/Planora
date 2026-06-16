using Planora.Shared.DTOs.Column;

namespace Planora.Shared.DTOs.Board;

public class BoardDetailDto : BoardDto
{
    public List<ColumnDto> Columns { get; set; } = [];
}
