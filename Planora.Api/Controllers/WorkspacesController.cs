using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Mappers;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
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
