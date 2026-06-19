using Riok.Mapperly.Abstractions;
using Planora.Api.Domain.Entities;
using Planora.Shared.DTOs.Board;
using Planora.Shared.DTOs.Card;
using Planora.Shared.DTOs.Checklist;
using Planora.Shared.DTOs.Column;
using Planora.Shared.DTOs.Label;
using Planora.Shared.DTOs.Workspace;

namespace Planora.Api.Application.Mappers;

[Mapper]
public static partial class PlanoraMappingProfile
{
    public static partial WorkspaceDto ToDto(this Workspace workspace);
    public static partial BoardDto ToDto(this Board board);
    public static partial BoardDetailDto ToDetailDto(this Board board);
    public static partial ColumnDto ToDto(this Column column);
    public static partial CardDto ToDto(this Card card);

    public static partial LabelDto ToDto(this WorkspaceLabel label);
    public static partial ChecklistDto ToDto(this Checklist checklist);
    public static partial ChecklistItemDto ToDto(this ChecklistItem item);

    // User-defined element conversion: Mapperly uses this when mapping ICollection<CardLabel> → List<LabelDto>
    private static LabelDto ToDto(CardLabel cl) => cl.Label.ToDto();

    public static partial IEnumerable<WorkspaceDto> ToDtoList(this IEnumerable<Workspace> workspaces);
    public static partial IEnumerable<BoardDto> ToDtoList(this IEnumerable<Board> boards);
    public static partial IEnumerable<ColumnDto> ToDtoList(this IEnumerable<Column> columns);
}
