using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Mappers;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
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

    public WorkspacesController(ApplicationDbContext db, IValidator<CreateWorkspaceRequest> createValidator)
    {
        _db = db;
        _createValidator = createValidator;
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
    public async Task<IActionResult> GetBoards(Guid id)
    {
        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == id && m.UserId == UserId);

        if (!isMember) return Forbid();

        var boards = await _db.Boards
            .Where(b => b.WorkspaceId == id)
            .OrderBy(b => b.Position)
            .ToListAsync();

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
