using Planora.Shared.Enums;

namespace Planora.Shared.DTOs.Notification;

public class NotificationDto
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public Guid? RelatedCardId { get; set; }
    public Guid? RelatedBoardId { get; set; }
    public Guid? RelatedWorkspaceId { get; set; }
    public DateTime CreatedAt { get; set; }
}
