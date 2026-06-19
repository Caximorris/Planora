using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Mappers;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.DTOs.Label;

namespace Planora.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LabelsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public LabelsController(ApplicationDbContext db) => _db = db;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // GET /api/labels/workspace/{workspaceId}
    [HttpGet("workspace/{workspaceId:guid}")]
    public async Task<IActionResult> GetByWorkspace(Guid workspaceId)
    {
        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        var labels = await _db.WorkspaceLabels
            .Where(l => l.WorkspaceId == workspaceId)
            .OrderBy(l => l.Name)
            .ToListAsync();

        return Ok(labels.Select(l => l.ToDto()));
    }

    // POST /api/labels/workspace/{workspaceId}
    [HttpPost("workspace/{workspaceId:guid}")]
    public async Task<IActionResult> Create(Guid workspaceId, [FromBody] CreateLabelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required.");

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        var label = new WorkspaceLabel
        {
            WorkspaceId = workspaceId,
            Name = request.Name.Trim(),
            Color = string.IsNullOrWhiteSpace(request.Color) ? "#94BFBE" : request.Color
        };

        _db.WorkspaceLabels.Add(label);
        await _db.SaveChangesAsync();
        return Ok(label.ToDto());
    }

    // PUT /api/labels/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLabelRequest request)
    {
        var label = await _db.WorkspaceLabels.FindAsync(id);
        if (label is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == label.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        if (request.Name is not null) label.Name = request.Name.Trim();
        if (request.Color is not null) label.Color = request.Color;

        await _db.SaveChangesAsync();
        return Ok(label.ToDto());
    }

    // DELETE /api/labels/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var label = await _db.WorkspaceLabels.FindAsync(id);
        if (label is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == label.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        _db.WorkspaceLabels.Remove(label);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/labels/{labelId}/cards/{cardId}  — attach label to card
    [HttpPost("{labelId:guid}/cards/{cardId:guid}")]
    public async Task<IActionResult> AttachToCard(Guid labelId, Guid cardId)
    {
        var label = await _db.WorkspaceLabels.FindAsync(labelId);
        if (label is null) return NotFound("Label not found.");

        var card = await _db.Cards
            .Include(c => c.Column).ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null) return NotFound("Card not found.");

        if (card.Column.Board.WorkspaceId != label.WorkspaceId)
            return BadRequest("Label does not belong to the card's workspace.");

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == label.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        var exists = await _db.CardLabels.AnyAsync(cl => cl.CardId == cardId && cl.LabelId == labelId);
        if (!exists)
        {
            _db.CardLabels.Add(new CardLabel { CardId = cardId, LabelId = labelId });
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    // DELETE /api/labels/{labelId}/cards/{cardId}  — detach label from card
    [HttpDelete("{labelId:guid}/cards/{cardId:guid}")]
    public async Task<IActionResult> DetachFromCard(Guid labelId, Guid cardId)
    {
        var entry = await _db.CardLabels
            .FirstOrDefaultAsync(cl => cl.CardId == cardId && cl.LabelId == labelId);

        if (entry is null) return NotFound();

        var label = await _db.WorkspaceLabels.FindAsync(labelId);
        if (label is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == label.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        _db.CardLabels.Remove(entry);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
