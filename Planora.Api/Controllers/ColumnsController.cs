using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Mappers;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.Constants;
using Planora.Shared.DTOs.Column;

namespace Planora.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ColumnsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<CreateColumnRequest> _createValidator;

    public ColumnsController(ApplicationDbContext db, IValidator<CreateColumnRequest> createValidator)
    {
        _db = db;
        _createValidator = createValidator;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateColumnRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var board = await _db.Boards.FirstOrDefaultAsync(b => b.Id == request.BoardId);
        if (board is null) return NotFound("Board not found.");

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        var maxPosition = await _db.Columns
            .Where(c => c.BoardId == request.BoardId)
            .MaxAsync(c => (int?)c.Position) ?? -1;

        var column = new Column
        {
            Title = request.Title,
            BoardId = request.BoardId,
            Position = maxPosition + 1
        };

        _db.Columns.Add(column);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Update), new { id = column.Id }, column.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateColumnRequest request)
    {
        var column = await _db.Columns
            .Include(c => c.Board)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (column is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        if (request.Title is not null) column.Title = request.Title;
        if (request.Position.HasValue) column.Position = request.Position.Value;
        if (request.ClearColor) column.Color = null;
        else if (request.Color is not null)
        {
            if (!PlanoraColors.TryNormalizeSafeSurfaceBackground(request.Color, out var color))
                return BadRequest("Color must be a readable card or column background color.");
            column.Color = color;
        }

        await _db.SaveChangesAsync();
        return Ok(column.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var column = await _db.Columns
            .Include(c => c.Board)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (column is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        _db.Columns.Remove(column);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
