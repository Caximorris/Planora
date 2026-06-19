using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Mappers;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.DTOs.Checklist;

namespace Planora.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChecklistsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public ChecklistsController(ApplicationDbContext db) => _db = db;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<bool> UserCanAccessCardAsync(Guid cardId)
    {
        var card = await _db.Cards
            .Include(c => c.Column).ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null) return false;
        return await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == UserId);
    }

    // POST /api/checklists  — create checklist on a card
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateChecklistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");

        if (!await UserCanAccessCardAsync(request.CardId))
            return Forbid();

        var maxPos = await _db.Checklists
            .Where(c => c.CardId == request.CardId)
            .MaxAsync(c => (int?)c.Position) ?? -1;

        var checklist = new Checklist
        {
            CardId = request.CardId,
            Title = request.Title.Trim(),
            Position = maxPos + 1
        };

        _db.Checklists.Add(checklist);
        await _db.SaveChangesAsync();
        return Ok(checklist.ToDto());
    }

    // PUT /api/checklists/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChecklistRequest request)
    {
        var checklist = await _db.Checklists.FindAsync(id);
        if (checklist is null) return NotFound();

        if (!await UserCanAccessCardAsync(checklist.CardId))
            return Forbid();

        if (request.Title is not null) checklist.Title = request.Title.Trim();
        await _db.SaveChangesAsync();
        return Ok(checklist.ToDto());
    }

    // DELETE /api/checklists/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var checklist = await _db.Checklists.FindAsync(id);
        if (checklist is null) return NotFound();

        if (!await UserCanAccessCardAsync(checklist.CardId))
            return Forbid();

        _db.Checklists.Remove(checklist);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/checklists/{id}/items
    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] CreateChecklistItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Text is required.");

        var checklist = await _db.Checklists.FindAsync(id);
        if (checklist is null) return NotFound();

        if (!await UserCanAccessCardAsync(checklist.CardId))
            return Forbid();

        var maxPos = await _db.ChecklistItems
            .Where(i => i.ChecklistId == id)
            .MaxAsync(i => (int?)i.Position) ?? -1;

        var item = new ChecklistItem
        {
            ChecklistId = id,
            Text = request.Text.Trim(),
            IsCompleted = false,
            Position = maxPos + 1
        };

        _db.ChecklistItems.Add(item);
        await _db.SaveChangesAsync();
        return Ok(item.ToDto());
    }

    // PUT /api/checklists/items/{id}
    [HttpPut("items/{id:guid}")]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] UpdateChecklistItemRequest request)
    {
        var item = await _db.ChecklistItems.FindAsync(id);
        if (item is null) return NotFound();

        var checklist = await _db.Checklists.FindAsync(item.ChecklistId);
        if (checklist is null) return NotFound();

        if (!await UserCanAccessCardAsync(checklist.CardId))
            return Forbid();

        if (request.Text is not null) item.Text = request.Text.Trim();
        if (request.IsCompleted.HasValue) item.IsCompleted = request.IsCompleted.Value;
        if (request.Position.HasValue) item.Position = request.Position.Value;

        await _db.SaveChangesAsync();
        return Ok(item.ToDto());
    }

    // DELETE /api/checklists/items/{id}
    [HttpDelete("items/{id:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        var item = await _db.ChecklistItems.FindAsync(id);
        if (item is null) return NotFound();

        var checklist = await _db.Checklists.FindAsync(item.ChecklistId);
        if (checklist is null) return NotFound();

        if (!await UserCanAccessCardAsync(checklist.CardId))
            return Forbid();

        _db.ChecklistItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
