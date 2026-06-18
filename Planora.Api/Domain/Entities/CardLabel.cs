namespace Planora.Api.Domain.Entities;

public class CardLabel
{
    public Guid CardId { get; set; }
    public Guid LabelId { get; set; }

    public Card Card { get; set; } = null!;
    public WorkspaceLabel Label { get; set; } = null!;
}
