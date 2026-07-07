namespace Planora.Api.Domain.Entities;

public class WorkspaceLabel : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = Planora.Shared.Constants.PlanoraColors.DefaultLabelColor;

    public Workspace Workspace { get; set; } = null!;
    public ICollection<CardLabel> CardLabels { get; set; } = [];
}
