using Planora.Shared.Enums;

namespace Planora.Shared.DTOs.Search;

public class SearchResultDto
{
    public SearchResultType Type { get; set; }
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public Guid BoardId { get; set; }
    public Guid WorkspaceId { get; set; }
}
