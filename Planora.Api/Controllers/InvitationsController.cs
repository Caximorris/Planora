using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.DTOs.Invitation;
using Planora.Shared.Enums;

namespace Planora.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvitationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public InvitationsController(ApplicationDbContext db) => _db = db;

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [AllowAnonymous]
    [HttpGet("{token}")]
    public async Task<IActionResult> GetByToken(string token)
    {
        var invitation = await _db.WorkspaceInvitations
            .Include(i => i.Workspace)
            .Include(i => i.Inviter)
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invitation is null) return NotFound();

        if (invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt < DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await _db.SaveChangesAsync();
        }

        return Ok(new InvitationDto
        {
            Id = invitation.Id,
            WorkspaceId = invitation.WorkspaceId,
            WorkspaceName = invitation.Workspace.Name,
            InviterName = invitation.Inviter.DisplayName,
            InviteeEmail = invitation.InviteeEmail,
            Role = invitation.Role,
            Status = invitation.Status,
            ExpiresAt = invitation.ExpiresAt,
            Token = invitation.Token
        });
    }

    [Authorize]
    [HttpPost("{token}/accept")]
    public async Task<IActionResult> Accept(string token)
    {
        var invitation = await _db.WorkspaceInvitations
            .Include(i => i.Workspace)
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invitation is null) return NotFound();

        if (invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt < DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await _db.SaveChangesAsync();
        }

        if (invitation.Status != InvitationStatus.Pending)
            return BadRequest($"This invitation is {invitation.Status.ToString().ToLower()} and can no longer be accepted.");

        var user = await _db.Users.FindAsync(UserId);
        if (user is null) return Unauthorized();

        if (!string.Equals(user.Email, invitation.InviteeEmail, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var alreadyMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == invitation.WorkspaceId && m.UserId == UserId);

        if (alreadyMember)
        {
            invitation.Status = InvitationStatus.Accepted;
            await _db.SaveChangesAsync();
            return Ok(new { workspaceId = invitation.WorkspaceId });
        }

        _db.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = invitation.WorkspaceId,
            UserId = UserId!,
            Role = invitation.Role,
            JoinedAt = DateTime.UtcNow
        });

        invitation.Status = InvitationStatus.Accepted;
        await _db.SaveChangesAsync();

        return Ok(new { workspaceId = invitation.WorkspaceId });
    }

    [Authorize]
    [HttpPost("{token}/decline")]
    public async Task<IActionResult> Decline(string token)
    {
        var invitation = await _db.WorkspaceInvitations
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invitation is null) return NotFound();

        if (invitation.Status != InvitationStatus.Pending)
            return BadRequest($"This invitation is already {invitation.Status.ToString().ToLower()}.");

        var user = await _db.Users.FindAsync(UserId);
        if (user is null) return Unauthorized();

        if (!string.Equals(user.Email, invitation.InviteeEmail, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        invitation.Status = InvitationStatus.Declined;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
