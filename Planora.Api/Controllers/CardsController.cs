using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Mappers;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.DTOs.Card;

namespace Planora.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CardsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<CreateCardRequest> _createValidator;

    public CardsController(ApplicationDbContext db, IValidator<CreateCardRequest> createValidator)
    {
        _db = db;
        _createValidator = createValidator;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var card = await _db.Cards
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (card is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        return Ok(card.ToDto());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCardRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var column = await _db.Columns
            .Include(c => c.Board)
            .FirstOrDefaultAsync(c => c.Id == request.ColumnId);

        if (column is null) return NotFound("Column not found.");

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        var maxPosition = await _db.Cards
            .Where(c => c.ColumnId == request.ColumnId)
            .MaxAsync(c => (int?)c.Position) ?? -1;

        var card = new Card
        {
            Title = request.Title,
            Description = request.Description,
            ColumnId = request.ColumnId,
            Priority = request.Priority,
            DueDate = request.DueDate,
            Position = maxPosition + 1
        };

        _db.Cards.Add(card);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = card.Id }, card.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCardRequest request)
    {
        var card = await _db.Cards
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (card is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        if (request.Title is not null) card.Title = request.Title;
        if (request.ClearDescription) card.Description = null;
        else if (request.Description is not null) card.Description = request.Description;
        if (request.Priority.HasValue) card.Priority = request.Priority.Value;
        if (request.DueDate.HasValue) card.DueDate = request.DueDate;
        if (request.Position.HasValue) card.Position = request.Position.Value;
        if (request.ClearAssignee) card.AssigneeId = null;
        else if (request.AssigneeId is not null) card.AssigneeId = request.AssigneeId;
        if (request.ClearColor) card.Color = null;
        else if (request.Color is not null) card.Color = request.Color;

        if (request.ColumnId.HasValue)
        {
            var targetColumn = await _db.Columns
                .FirstOrDefaultAsync(c => c.Id == request.ColumnId.Value && c.BoardId == card.Column.BoardId);
            if (targetColumn is null)
                return BadRequest("Target column does not belong to the same board.");
            card.ColumnId = request.ColumnId.Value;
        }

        await _db.SaveChangesAsync();
        return Ok(card.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var card = await _db.Cards
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (card is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        _db.Cards.Remove(card);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
