using Planora.Shared.DTOs.Column;

namespace Planora.Shared.Filtering;

public class BoardFilterResult
{
    public static BoardFilterResult Empty { get; } = new();

    public List<ColumnDto> Columns { get; init; } = [];
    public int TotalCards { get; init; }
    public int MatchingCards { get; init; }
    public bool HasActiveFilters { get; init; }
}
