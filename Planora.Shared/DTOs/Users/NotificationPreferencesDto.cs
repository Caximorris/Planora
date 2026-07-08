namespace Planora.Shared.DTOs.Users;

/// <summary>Per-user email-notification toggles (all default on). Used for both GET and PUT.</summary>
public class NotificationPreferencesDto
{
    public bool EmailOnAssigned { get; set; } = true;
    public bool EmailOnComment { get; set; } = true;
    public bool EmailOnWorkspaceInvite { get; set; } = true;
}
