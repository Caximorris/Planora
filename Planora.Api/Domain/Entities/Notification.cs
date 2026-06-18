using Planora.Shared.Enums;

namespace Planora.Api.Domain.Entities;

public class Notification : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public Guid? RelatedCardId { get; set; }
    public Guid? RelatedWorkspaceId { get; set; }

    public AppUser User { get; set; } = null!;
}
