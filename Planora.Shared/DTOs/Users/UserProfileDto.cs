namespace Planora.Shared.DTOs.Users;

public class UserProfileDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
}
