using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.DTOs.Notification;

namespace Planora.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public NotificationsController(ApplicationDbContext db) => _db = db;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 15)
    {
        var notifications = await _db.Notifications
            .Where(n => n.UserId == UserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Message = n.Message,
                IsRead = n.IsRead,
                RelatedCardId = n.RelatedCardId,
                RelatedBoardId = n.RelatedBoardId,
                RelatedWorkspaceId = n.RelatedWorkspaceId,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _db.Notifications
            .CountAsync(n => n.UserId == UserId && !n.IsRead);
        return Ok(count);
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);
        if (notification is null) return NotFound();

        notification.IsRead = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _db.Notifications
            .Where(n => n.UserId == UserId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);
        if (notification is null) return NotFound();

        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
