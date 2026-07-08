using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Api.Application.Interfaces;
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
    private static readonly Dictionary<string, string> AllowedAttachmentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
        ["application/pdf"] = ".pdf",
        ["text/plain"] = ".txt",
    };
    private const string AttachmentRelativeDir = "uploads/cards";

    private readonly ApplicationDbContext _db;
    private readonly IValidator<CreateCardRequest> _createValidator;
    private readonly IFileStorage _storage;

    public CardsController(ApplicationDbContext db, IValidator<CreateCardRequest> createValidator, IFileStorage storage)
    {
        _db = db;
        _createValidator = createValidator;
        _storage = storage;
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
            .Include(c => c.Attachments.OrderBy(a => a.CreatedAt))
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
        if (request.RowVersion == 0) return BadRequest("RowVersion is required.");

        var card = await _db.Cards
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (card is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        _db.Entry(card).Property(c => c.RowVersion).OriginalValue = request.RowVersion;

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

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Card was modified by another request. Reload and try again." });
        }

        return Ok(card.ToDto());
    }

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(CardLimits.MaxAttachmentBytes + 1024)]
    public async Task<IActionResult> UploadAttachment(Guid id, IFormFile? file)
    {
        var card = await _db.Cards
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (card is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");
        if (file.Length > CardLimits.MaxAttachmentBytes)
            return BadRequest($"Attachment must be smaller than {CardLimits.MaxAttachmentBytes / 1024 / 1024} MB.");
        if (!AllowedAttachmentTypes.TryGetValue(file.ContentType, out var extension))
            return BadRequest("Unsupported attachment type. Use PNG, JPEG, WEBP, GIF, PDF or TXT.");

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        buffer.Position = 0;
        var header = new byte[Math.Min(16, (int)buffer.Length)];
        await buffer.ReadExactlyAsync(header);
        buffer.Position = 0;

        if (!HasValidAttachmentSignature(header, file.ContentType))
            return BadRequest("File content doesn't match a supported attachment format.");

        var url = await _storage.SaveAsync(buffer, AttachmentRelativeDir, extension);
        var attachment = new CardAttachment
        {
            CardId = card.Id,
            UploadedById = UserId,
            FileName = SafeDisplayFileName(file.FileName),
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            Url = url
        };

        _db.CardAttachments.Add(attachment);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            await _storage.DeleteAsync(url);
            throw;
        }

        return Ok(attachment.ToDto());
    }

    [HttpDelete("{cardId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid cardId, Guid attachmentId)
    {
        var attachment = await _db.CardAttachments
            .Include(a => a.Card)
                .ThenInclude(c => c.Column)
                    .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.CardId == cardId);
        if (attachment is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == attachment.Card.Column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        var url = attachment.Url;
        _db.CardAttachments.Remove(attachment);
        await _db.SaveChangesAsync();
        await _storage.DeleteAsync(url);

        return NoContent();
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
        // DeletedAt == null: a trashed card is restored via /restore, not unarchived here.
        var card = await _db.Cards
            .IgnoreQueryFilters()
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null);

        if (card is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        card.IsArchived = false;
        await _db.SaveChangesAsync();
        return Ok(card.ToDto());
    }

    // Soft delete: move the card to its board's trash (recoverable).
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var card = await _db.Cards
            .IgnoreQueryFilters()
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null);

        if (card is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        card.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("trash")]
    public async Task<IActionResult> GetTrash([FromQuery] Guid boardId)
    {
        var workspaceId = await _db.Boards
            .IgnoreQueryFilters()
            .Where(b => b.Id == boardId)
            .Select(b => (Guid?)b.WorkspaceId)
            .FirstOrDefaultAsync();
        if (workspaceId is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        var cards = await _db.Cards
            .IgnoreQueryFilters()
            .Include(c => c.Labels).ThenInclude(cl => cl.Label)
            .Where(c => c.Column.BoardId == boardId && c.DeletedAt != null)
            .OrderByDescending(c => c.DeletedAt)
            .ToListAsync();

        return Ok(cards.Select(c => c.ToDto()).ToList());
    }

    [HttpPatch("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id)
    {
        var card = await _db.Cards
            .IgnoreQueryFilters()
            .Include(c => c.Attachments)
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt != null);

        if (card is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        card.DeletedAt = null;
        await _db.SaveChangesAsync();
        return Ok(card.ToDto());
    }

    // Hard delete — only a trashed card can be permanently removed.
    [HttpDelete("{id:guid}/permanent")]
    public async Task<IActionResult> DeletePermanent(Guid id)
    {
        var card = await _db.Cards
            .IgnoreQueryFilters()
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt != null);

        if (card is null) return NotFound();

        var isMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == card.Column.Board.WorkspaceId && m.UserId == UserId);
        if (!isMember) return Forbid();

        foreach (var attachment in card.Attachments)
            await _storage.DeleteAsync(attachment.Url);

        _db.Cards.Remove(card);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static bool HasValidAttachmentSignature(byte[] header, string contentType) => contentType switch
    {
        "image/png" => header.Length >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
        "image/jpeg" => header.Length >= 3
            && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
        "image/gif" => header.Length >= 6
            && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38
            && (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61,
        "image/webp" => header.Length >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
        "application/pdf" => header.Length >= 5
            && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46 && header[4] == 0x2D,
        "text/plain" => header.All(b => b != 0),
        _ => false,
    };

    private static string SafeDisplayFileName(string? fileName)
    {
        var safeName = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(safeName))
            return "attachment";

        safeName = string.Concat(safeName.Select(ch => char.IsControl(ch) ? '_' : ch));
        return safeName.Length <= 180 ? safeName : safeName[..180];
    }
}
