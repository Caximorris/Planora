using Riok.Mapperly.Abstractions;
using Planora.Api.Domain.Entities;
using Planora.Shared.DTOs.Board;
using Planora.Shared.DTOs.Card;
using Planora.Shared.DTOs.Column;
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

    public static partial IEnumerable<WorkspaceDto> ToDtoList(this IEnumerable<Workspace> workspaces);
    public static partial IEnumerable<BoardDto> ToDtoList(this IEnumerable<Board> boards);
    public static partial IEnumerable<ColumnDto> ToDtoList(this IEnumerable<Column> columns);
}
