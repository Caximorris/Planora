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
    private readonly IValidator<UpdateColumnRequest> _updateValidator;

    public ColumnsController(
        ApplicationDbContext db,
        IValidator<CreateColumnRequest> createValidator,
        IValidator<UpdateColumnRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
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
        if (request.RowVersion == 0) return BadRequest("RowVersion is required.");

        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var column = await _db.Columns
            .Include(c => c.Board)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (column is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        _db.Entry(column).Property(c => c.RowVersion).OriginalValue = request.RowVersion;

        if (request.Title is not null) column.Title = request.Title;
        if (request.Position.HasValue) column.Position = request.Position.Value;
        if (request.ClearColor) column.Color = null;
        else if (request.Color is not null)
        {
            if (!PlanoraColors.TryNormalizeSafeSurfaceBackground(request.Color, out var color))
                return BadRequest("Color must be a readable card or column background color.");
            column.Color = color;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Column was modified by another request. Reload and try again." });
        }

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
