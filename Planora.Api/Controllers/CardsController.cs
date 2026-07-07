using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Mappers;
using Planora.Api.Domain.Entities;
using Planora.Api.Infrastructure.Data;
using Planora.Shared.Constants;
using Planora.Shared.DTOs.Card;
using Planora.Shared.Enums;

namespace Planora.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CardsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
            .Include(c => c.Labels)
                .ThenInclude(cl => cl.Label)
            .Include(c => c.Checklists.OrderBy(ch => ch.Position))
                .ThenInclude(ch => ch.Items.OrderBy(i => i.Position))
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
        _db.ActivityEvents.Add(new ActivityEvent
        {
            ActorUserId = UserId,
            Verb = "card.created",
            TargetType = "card",
            TargetId = card.Id,
            WorkspaceId = column.Board.WorkspaceId,
            BoardId = column.BoardId,
            PayloadJson = ToPayloadJson(new
            {
                title = card.Title,
                columnId = card.ColumnId,
                columnTitle = column.Title,
                position = card.Position
            })
        });
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

        var previousAssigneeId = card.AssigneeId;
        var previousColumnId = card.ColumnId;
        var previousColumnTitle = card.Column.Title;
        var previousPosition = card.Position;

        if (request.Title is not null) card.Title = request.Title;
        if (request.ClearDescription) card.Description = null;
        else if (request.Description is not null) card.Description = request.Description;
        if (request.Priority.HasValue) card.Priority = request.Priority.Value;
        if (request.DueDate.HasValue) card.DueDate = request.DueDate;
        if (request.Position.HasValue) card.Position = request.Position.Value;

        if (request.ClearAssignee)
        {
            card.AssigneeId = null;
        }
        else if (request.AssigneeId is not null)
        {
            var isAssigneeMember = await _db.WorkspaceMembers
                .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == request.AssigneeId);
            if (!isAssigneeMember)
                return BadRequest("Assignee must be a member of this workspace.");
            card.AssigneeId = request.AssigneeId;
        }

        if (request.ClearColor) card.Color = null;
        else if (request.Color is not null)
        {
            if (!PlanoraColors.TryNormalizeSafeSurfaceBackground(request.Color, out var color))
                return BadRequest("Color must be a readable card or column background color.");
            card.Color = color;
        }

        Column? targetColumn = null;
        if (request.ColumnId.HasValue)
        {
            targetColumn = await _db.Columns
                .FirstOrDefaultAsync(c => c.Id == request.ColumnId.Value && c.BoardId == card.Column.BoardId);
            if (targetColumn is null)
                return BadRequest("Target column does not belong to the same board.");
            card.ColumnId = request.ColumnId.Value;
        }

        var columnChanged = card.ColumnId != previousColumnId;
        var positionChanged = card.Position != previousPosition;
        if (columnChanged || positionChanged)
        {
            _db.ActivityEvents.Add(new ActivityEvent
            {
                ActorUserId = UserId,
                Verb = "card.moved",
                TargetType = "card",
                TargetId = card.Id,
                WorkspaceId = card.Column.Board.WorkspaceId,
                BoardId = card.Column.BoardId,
                PayloadJson = ToPayloadJson(new
                {
                    title = card.Title,
                    fromColumnId = previousColumnId,
                    fromColumnTitle = previousColumnTitle,
                    toColumnId = card.ColumnId,
                    toColumnTitle = targetColumn?.Title ?? previousColumnTitle,
                    fromPosition = previousPosition,
                    toPosition = card.Position
                })
            });
        }

        // Notify the assignee when the card is moved to a different column (and they didn't move it themselves)
        if (columnChanged
            && card.AssigneeId is not null
            && card.AssigneeId != UserId)
        {
            var targetColumnName = targetColumn?.Title ?? "another column";

            _db.Notifications.Add(new Notification
            {
                UserId = card.AssigneeId,
                Type = NotificationType.CardMoved,
                Message = $"\"{card.Title}\" was moved to \"{targetColumnName}\"",
                RelatedCardId = card.Id,
                RelatedBoardId = card.Column.BoardId,
                RelatedWorkspaceId = card.Column.Board.WorkspaceId
            });
        }

        // Notify the new assignee if it changed (and they didn't assign themselves)
        if (card.AssigneeId is not null
            && card.AssigneeId != previousAssigneeId
            && card.AssigneeId != UserId)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = card.AssigneeId,
                Type = NotificationType.AssignedToCard,
                Message = $"You were assigned to \"{card.Title}\"",
                RelatedCardId = card.Id,
                RelatedBoardId = card.Column.BoardId,
                RelatedWorkspaceId = card.Column.Board.WorkspaceId
            });
        }

        await _db.SaveChangesAsync();
        return Ok(card.ToDto());
    }

    private static string ToPayloadJson<T>(T payload) => JsonSerializer.Serialize(payload, JsonOptions);

    [HttpPatch("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id)
    {
        var card = await _db.Cards
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (card is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        card.IsArchived = true;
        await _db.SaveChangesAsync();
        return Ok(card.ToDto());
    }

    [HttpPatch("{id:guid}/unarchive")]
    public async Task<IActionResult> Unarchive(Guid id)
    {
        var card = await _db.Cards
            .IgnoreQueryFilters()
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (card is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        card.IsArchived = false;
        await _db.SaveChangesAsync();
        return Ok(card.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var card = await _db.Cards
            .IgnoreQueryFilters()
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
