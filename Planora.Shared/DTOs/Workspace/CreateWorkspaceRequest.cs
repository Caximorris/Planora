namespace Planora.Shared.DTOs.Workspace;

public class CreateWorkspaceRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
