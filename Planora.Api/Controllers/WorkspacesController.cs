using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Interfaces;
using Planora.Api.Application.Mappers;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.DTOs.Calendar;
using Planora.Shared.DTOs.Invitation;
using Planora.Shared.DTOs.Workspace;
using Planora.Shared.Enums;

namespace Planora.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class WorkspacesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<CreateWorkspaceRequest> _createValidator;
    private readonly IValidator<UpdateWorkspaceRequest> _updateValidator;
    private readonly IActivityEmailNotifier _emailNotifier;

    public WorkspacesController(
        ApplicationDbContext db,
        IValidator<CreateWorkspaceRequest> createValidator,
        IValidator<UpdateWorkspaceRequest> updateValidator,
        IActivityEmailNotifier emailNotifier)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _emailNotifier = emailNotifier;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var workspaces = await _db.Workspaces
            .Where(w => w.Members.Any(m => m.UserId == UserId))
            .OrderBy(w => w.CreatedAt)
            .ToListAsync();

        return Ok(workspaces.ToDtoList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var workspace = await _db.Workspaces
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workspace is null) return NotFound();
        if (!workspace.Members.Any(m => m.UserId == UserId)) return Forbid();

        return Ok(workspace.ToDto());
    }

    [HttpGet("{id:guid}/boards")]
    public async Task<IActionResult> GetBoards(Guid id, [FromQuery] bool includeArchived = false)
    {
        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == id && m.UserId == UserId);

        if (!isMember) return Forbid();

        var boardsQuery = includeArchived
            // Show archived boards too, but never trashed ones (those live in the workspace trash).
            ? _db.Boards.IgnoreQueryFilters().Where(b => b.WorkspaceId == id && b.DeletedAt == null)
            : _db.Boards.Where(b => b.WorkspaceId == id);

        var boards = await boardsQuery.OrderBy(b => b.Position).ToListAsync();

        return Ok(boards.ToDtoList());
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == id && m.UserId == UserId);

        if (!isMember) return Forbid();

        var members = await _db.WorkspaceMembers
            .Include(m => m.User)
            .Where(m => m.WorkspaceId == id)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync();

        var dtos = members.Select(m => new WorkspaceMemberDto
        {
            UserId = m.UserId,
            DisplayName = m.User.DisplayName,
            Email = m.User.Email ?? string.Empty,
            AvatarUrl = m.User.AvatarUrl,
            Role = m.Role,
            JoinedAt = m.JoinedAt
        });

        return Ok(dtos);
    }

    [HttpDelete("{id:guid}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(Guid id, string userId)
    {
        var callerMember = await _db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == id && m.UserId == UserId);

        if (callerMember is null) return Forbid();
        if (callerMember.Role != WorkspaceRole.Owner && callerMember.Role != WorkspaceRole.Admin)
            return Forbid();

        // Owner cannot be removed
        var workspace = await _db.Workspaces.FindAsync(id);
        if (workspace?.OwnerId == userId)
            return BadRequest("The workspace owner cannot be removed.");

        var target = await _db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == id && m.UserId == userId);

        if (target is null) return NotFound();

        _db.WorkspaceMembers.Remove(target);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:guid}/members/{userId}")]
    public async Task<IActionResult> UpdateMemberRole(Guid id, string userId, [FromBody] UpdateMemberRoleRequest request)
    {
        var callerMember = await _db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == id && m.UserId == UserId);

        if (callerMember is null || callerMember.Role != WorkspaceRole.Owner) return Forbid();

        var workspace = await _db.Workspaces.FindAsync(id);
        if (workspace?.OwnerId == userId)
            return BadRequest("Cannot change the role of the workspace owner.");

        if (request.Role == WorkspaceRole.Owner)
            return BadRequest("Cannot assign Owner role via this endpoint.");

        var target = await _db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == id && m.UserId == userId);

        if (target is null) return NotFound();

        target.Role = request.Role;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(Guid id, [FromBody] TransferWorkspaceOwnershipRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewOwnerUserId))
            return BadRequest("New owner is required.");

        var workspace = await _db.Workspaces
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workspace is null) return NotFound();

        var callerMember = workspace.Members.FirstOrDefault(m => m.UserId == UserId);
        if (callerMember is null) return Forbid();
        if (workspace.OwnerId != UserId || callerMember.Role != WorkspaceRole.Owner)
            return Forbid();

        if (request.NewOwnerUserId == UserId)
            return BadRequest("You already own this workspace.");

        var newOwner = workspace.Members.FirstOrDefault(m => m.UserId == request.NewOwnerUserId);
        if (newOwner is null)
            return NotFound("New owner must already be a workspace member.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        workspace.OwnerId = newOwner.UserId;
        newOwner.Role = WorkspaceRole.Owner;
        callerMember.Role = WorkspaceRole.Admin;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(workspace.ToDto());
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id)
    {
        var workspace = await _db.Workspaces
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workspace is null) return NotFound();

        var member = workspace.Members.FirstOrDefault(m => m.UserId == UserId);
        if (member is null) return Forbid();

        if (workspace.OwnerId == UserId)
            return BadRequest("Transfer ownership before leaving this workspace.");

        _db.WorkspaceMembers.Remove(member);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/calendar")]
    public async Task<IActionResult> GetCalendar(Guid id, [FromQuery] string? month)
    {
        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == id && m.UserId == UserId);
        if (!isMember) return Forbid();

        // Parse month as "yyyy-MM"; default to current month
        DateTime start;
        if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParseExact(month + "-01", "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var parsed))
        {
            start = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }
        else
        {
            var now = DateTime.UtcNow;
            start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        var end = start.AddMonths(1);
        var now2 = DateTime.UtcNow;

        var cards = await _db.Cards
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .Include(c => c.Assignee)
            .Where(c => !c.IsArchived
                && c.DueDate.HasValue
                && c.DueDate >= start
                && c.DueDate < end
                && c.Column.Board.WorkspaceId == id
                && !c.Column.Board.IsArchived)
            .OrderBy(c => c.DueDate)
            .ToListAsync();

        var dtos = cards.Select(c => new CalendarCardDto
        {
            Id = c.Id,
            Title = c.Title,
            DueDate = c.DueDate!.Value,
            BoardId = c.Column.BoardId,
            BoardName = c.Column.Board.Name,
            ColumnName = c.Column.Title,
            Priority = c.Priority,
            IsOverdue = c.DueDate < now2,
            AssigneeDisplayName = c.Assignee?.DisplayName
        });

        return Ok(dtos);
    }

    [HttpGet("{id:guid}/invitations")]
    public async Task<IActionResult> GetInvitations(Guid id)
    {
        var callerMember = await _db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == id && m.UserId == UserId);

        if (callerMember is null || (callerMember.Role != WorkspaceRole.Owner && callerMember.Role != WorkspaceRole.Admin))
            return Forbid();

        // Mark stale pending invitations as expired so the list reflects reality.
        var stale = await _db.WorkspaceInvitations
            .Where(i => i.WorkspaceId == id && i.Status == InvitationStatus.Pending && i.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();
        if (stale.Count > 0)
        {
            foreach (var s in stale) s.Status = InvitationStatus.Expired;
            await _db.SaveChangesAsync();
        }

        var invitations = await _db.WorkspaceInvitations
            .Include(i => i.Workspace)
            .Include(i => i.Inviter)
            .Where(i => i.WorkspaceId == id && i.Status == InvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        var dtos = invitations.Select(i => new InvitationDto
        {
            Id = i.Id,
            WorkspaceId = i.WorkspaceId,
            WorkspaceName = i.Workspace.Name,
            InviterName = i.Inviter != null ? i.Inviter.DisplayName : "Someone",
            InviteeEmail = i.InviteeEmail,
            Role = i.Role,
            Status = i.Status,
            ExpiresAt = i.ExpiresAt,
            Token = i.Token
        });

        return Ok(dtos);
    }

    [HttpPost("{id:guid}/invitations")]
    public async Task<IActionResult> CreateInvitation(Guid id, [FromBody] CreateInvitationRequest request)
    {
        var callerMember = await _db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == id && m.UserId == UserId);

        if (callerMember is null || (callerMember.Role != WorkspaceRole.Owner && callerMember.Role != WorkspaceRole.Admin))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.InviteeEmail))
            return BadRequest("Email is required.");

        var email = request.InviteeEmail.Trim().ToLowerInvariant();

        // Already a member?
        var alreadyMember = await _db.Users
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
            .Join(_db.WorkspaceMembers.Where(m => m.WorkspaceId == id),
                  u => u.Id, m => m.UserId, (u, m) => m)
            .AnyAsync();

        if (alreadyMember)
            return Conflict("This user is already a member of the workspace.");

        // Cancel any existing pending invitation for this email
        var existing = await _db.WorkspaceInvitations
            .Where(i => i.WorkspaceId == id && i.InviteeEmail == email && i.Status == InvitationStatus.Pending)
            .FirstOrDefaultAsync();

        if (existing is not null)
            _db.WorkspaceInvitations.Remove(existing);

        var workspace = await _db.Workspaces.FindAsync(id);
        if (workspace is null) return NotFound();

        var inviter = await _db.Users.FindAsync(UserId);

        var invitation = new WorkspaceInvitation
        {
            WorkspaceId = id,
            InviterUserId = UserId,
            InviteeEmail = email,
            Role = request.Role == WorkspaceRole.Owner ? WorkspaceRole.Member : request.Role,
            Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = InvitationStatus.Pending
        };

        _db.WorkspaceInvitations.Add(invitation);
        await _db.SaveChangesAsync();

        await _emailNotifier.NotifyWorkspaceInviteAsync(
            email, workspace.Name, inviter?.DisplayName ?? "Someone", invitation.Token, HttpContext.RequestAborted);

        return Ok(new InvitationDto
        {
            Id = invitation.Id,
            WorkspaceId = invitation.WorkspaceId,
            WorkspaceName = workspace.Name,
            InviterName = inviter?.DisplayName ?? "Someone",
            InviteeEmail = invitation.InviteeEmail,
            Role = invitation.Role,
            Status = invitation.Status,
            ExpiresAt = invitation.ExpiresAt,
            Token = invitation.Token
        });
    }

    [HttpDelete("{id:guid}/invitations/{invitationId:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid id, Guid invitationId)
    {
        var callerMember = await _db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == id && m.UserId == UserId);

        if (callerMember is null || (callerMember.Role != WorkspaceRole.Owner && callerMember.Role != WorkspaceRole.Admin))
            return Forbid();

        var invitation = await _db.WorkspaceInvitations
            .FirstOrDefaultAsync(i => i.WorkspaceId == id && i.Id == invitationId);

        if (invitation is null) return NotFound();

        if (invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt < DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await _db.SaveChangesAsync();
            return BadRequest("This invitation is expired and can no longer be revoked.");
        }

        if (invitation.Status != InvitationStatus.Pending)
            return BadRequest($"This invitation is already {invitation.Status.ToString().ToLower()}.");

        invitation.Status = InvitationStatus.Revoked;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkspaceRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var workspace = new Workspace
        {
            Name = request.Name,
            Description = request.Description,
            OwnerId = UserId
        };

        workspace.Members.Add(new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = UserId,
            Role = WorkspaceRole.Owner,
            JoinedAt = DateTime.UtcNow
        });

        _db.Workspaces.Add(workspace);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = workspace.Id }, workspace.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkspaceRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var workspace = await _db.Workspaces
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workspace is null) return NotFound();

        var member = workspace.Members.FirstOrDefault(m => m.UserId == UserId);
        if (member is null || (member.Role != WorkspaceRole.Owner && member.Role != WorkspaceRole.Admin))
            return Forbid();

        if (request.Name is not null) workspace.Name = request.Name;
        if (request.Description is not null) workspace.Description = request.Description;

        await _db.SaveChangesAsync();
        return Ok(workspace.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var workspace = await _db.Workspaces
            .FirstOrDefaultAsync(w => w.Id == id && w.OwnerId == UserId);

        if (workspace is null) return NotFound();

        _db.Workspaces.Remove(workspace);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
